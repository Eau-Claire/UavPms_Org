using UavPms.OperationsService.Infrastructure.Repositories;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class MissionLifecycleService : IMissionLifecycleService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _current;
    private readonly IMissionRealtimeNotifier? _notifier;

    public MissionLifecycleService(
        ApplicationDbContext db,
        ICurrentUserServices current,
        IMissionRealtimeNotifier? notifier = null)
    {
        _db = db;
        _current = current;
        _notifier = notifier;
    }

    private async Task<Guid?> ResolveUserIdAsync(Guid? id, CancellationToken ct)
    {
        if (!id.HasValue || id.Value == Guid.Empty) return null;
        if (await _db.Users.AnyAsync(u => u.Id == id.Value, ct)) return id.Value;
        var cand = await _db.PreMissionAssessmentPersonnel.FirstOrDefaultAsync(p => p.Id == id.Value, ct);
        if (cand != null && await _db.Users.AnyAsync(u => u.Id == cand.UserId, ct)) return cand.UserId;
        return null;
    }

    private async Task<Guid?> ResolveDroneIdAsync(Guid? id, CancellationToken ct)
    {
        if (!id.HasValue || id.Value == Guid.Empty) return null;
        if (await _db.Uavs.AnyAsync(d => d.Id == id.Value, ct)) return id.Value;
        var cand = await _db.PreMissionAssessmentDrones.FirstOrDefaultAsync(d => d.Id == id.Value, ct);
        if (cand != null && await _db.Uavs.AnyAsync(d => d.Id == cand.DroneId, ct)) return cand.DroneId;
        return null;
    }

    public async Task<IReadOnlyList<Tower>> ResolveScopeAsync(Guid missionId, string boundaryWkt, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct);
        var boundary = ParseBoundarySafe(boundaryWkt) ?? mission.Boundary;
        var regionId = mission.RegionId ?? throw new BusinessRuleException("MISSION_REGION_REQUIRED");
        if (boundary == null)
        {
            return await AssetsForRegion(regionId).ToListAsync(ct);
        }
        return await AssetsForRegion(regionId).Where(x => x.Geom != null && boundary.Covers(x.Geom)).ToListAsync(ct);
    }

    public async Task ConfirmAssetsAsync(Guid missionId, string boundaryWkt, IReadOnlyCollection<Guid> towerIds, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct); RequirePreExecution(mission);
        if (towerIds.Count == 0) throw new BusinessRuleException("MISSION_TARGET_REQUIRED");
        var distinct = towerIds.Distinct().ToArray(); if (distinct.Length != towerIds.Count) throw new BusinessRuleException("DUPLICATE_TOWER");
        var regionId = mission.RegionId ?? throw new BusinessRuleException("MISSION_REGION_REQUIRED");
        var towers = await AssetsForRegion(regionId).Where(x => distinct.Contains(x.Id)).ToListAsync(ct);
        if (towers.Count != distinct.Length) throw new ForbiddenException("TOWER_OUTSIDE_REGION_OR_SCOPE");

        // BR-08: Scope Consistency - Significant scope changes require MF01 reassessment
        if (mission.PreMissionAssessmentId.HasValue)
        {
            var assessedAssetIds = await _db.PreMissionAssessmentTowers
                .Where(a => a.AssessmentId == mission.PreMissionAssessmentId.Value)
                .Select(a => a.TowerId)
                .ToListAsync(ct);
            if (assessedAssetIds.Count > 0 && distinct.Except(assessedAssetIds).Any())
            {
                throw new BusinessRuleException("SCOPE_REASSESSMENT_REQUIRED",
                    "Significant scope changes detected (towers outside MF01 assessment). Reassessment via MF01 is required.");
            }
        }

        var boundary = ParseBoundarySafe(boundaryWkt) ?? mission.Boundary;
        if (boundary == null && towers.Any(x => x.Geom != null))
        {
            var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(4326);
            var validPoints = towers.Where(x => x.Geom != null).Select(x => x.Geom is Point pt ? pt : factory.CreatePoint(x.Geom!.Coordinate)).ToArray();
            var mp = factory.CreateMultiPoint(validPoints);
            var hull = mp.ConvexHull();
            boundary = (hull is Polygon poly) ? poly.Buffer(0.002) : hull.Buffer(0.002);
            boundary.SRID = 4326;
        }

        _db.MissionTargets.RemoveRange(await _db.MissionTargets.Where(x => x.MissionId == mission.Id).ToListAsync(ct));
        _db.MissionTargets.AddRange(distinct.Select((id, i) => new MissionTarget { MissionId = mission.Id, TowerId = id, Sequence = i + 1 }));
        if (boundary != null)
        {
            mission.Boundary = boundary;
            Audit(mission.Id, "MISSION_SCOPE_CHANGED");
        }
        Audit(mission.Id, "MISSION_ASSETS_CHANGED");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MissionAssignment> AssignAsync(Guid missionId, Mf01Assignment request, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct, true); RequirePreExecution(mission);
        if (request.UserId == Guid.Empty) throw new BusinessRuleException("ASSIGNEE_REQUIRED", "UserId is required.");
        var role = string.IsNullOrWhiteSpace(request.AssignmentRole) ? "INSPECTOR" : request.AssignmentRole.Trim();

        // Adding someone who is already on the mission only updates the role; it never books them twice.
        var existing = mission.ActiveAssignments().FirstOrDefault(x => x.UserId == request.UserId);
        if (existing != null)
        {
            existing.AssignmentRole = role;
            if (IsInspectorRole(role) && !mission.PrimaryInspectorId().HasValue)
            {
                mission.AssignedToUserId = request.UserId;
            }
            await SaveConcurrency(ct);
            return existing;
        }

        // BR-18/BR-19 re-validation: Active, verified, field role, in scope and not booked in the window.
        var (plannedStart, plannedEnd) = PlannedWindow(mission);
        if (plannedStart.HasValue && plannedEnd.HasValue)
        {
            await MissionResourceGuard.EnsurePersonEligibleAsync(_db, request.UserId, RequireRegion(mission), plannedStart.Value, plannedEnd.Value, mission.Id, IsGlobal, ct);
            _db.ResourceBookings.Add(new ResourceBooking
            {
                MissionId = missionId,
                UserId = request.UserId,
                StartAt = plannedStart.Value,
                EndAt = plannedEnd.Value,
                Status = ResourceBookingStatus.Active
            });
        }
        else
        {
            var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == request.UserId, ct) ?? throw new NotFoundException("User", request.UserId);
            if (!IsActive(user.Status)) throw new BusinessRuleException("ASSIGNEE_INACTIVE");
        }

        var assignment = new MissionAssignment
        {
            MissionId = missionId,
            UserId = request.UserId,
            AssignmentRole = role,
            IsRequired = request.IsRequired,
            AssignedById = _current.UserId
        };
        _db.MissionAssignments.Add(assignment);
        if (!mission.Assignments.Contains(assignment))
        {
            mission.Assignments.Add(assignment);
        }
        if (IsInspectorRole(role) && !mission.PrimaryInspectorId().HasValue)
        {
            mission.AssignedToUserId = request.UserId;
        }

        // BR-32: a new required member who has not accepted sends a dispatched mission back to Pending Acceptance.
        mission.RecalculateReadiness();
        TouchMission(mission);
        Notify(request.UserId, mission, "MISSION_ASSIGNED", role); Audit(mission.Id, "MISSION_ASSIGNMENT_ADDED");
        await SaveConcurrency(ct); return assignment;
    }

    public async Task RemoveAssignmentAsync(Guid missionId, Guid assignmentId, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct, true); RequirePreExecution(mission);
        var assignment = mission.ActiveAssignments().SingleOrDefault(x => x.Id == assignmentId)
            ?? throw new NotFoundException("MissionAssignment", assignmentId);

        // A mission keeps at least one required member, otherwise it could never be accepted (BR-26, BR-30).
        if (assignment.IsRequired && !mission.ActiveAssignments().Any(a => a.Id != assignment.Id && a.IsRequired))
            throw new BusinessRuleException("REQUIRED_MEMBER_REQUIRED", "The last required member cannot be removed; reassign it instead.");

        assignment.Status = MissionAssignmentStatus.Revoked;
        assignment.ResponseStatus = MissionAssignmentResponse.Cancelled;
        assignment.EndedAt = DateTime.UtcNow;
        assignment.Version++;
        CloseMemberFieldRecords(mission, assignment.UserId);

        // Table 60: a removed member's booking is Cancelled.
        await MissionResourceGuard.EndActiveBookingsAsync(_db, missionId, ResourceBookingStatus.Cancelled, ct, b => b.UserId == assignment.UserId);

        // Removing the last pending required member may complete the acceptance (BR-30).
        if (mission.Status == MissionStatus.PendingAcceptance)
        {
            if (mission.CheckAcceptance()) Audit(mission.Id, "MISSION_CONFIRMED");
        }
        else
        {
            mission.RecalculateReadiness();
        }
        TouchMission(mission);

        Notify(assignment.UserId, mission, "MISSION_ASSIGNMENT_REMOVED", assignment.AssignmentRole); Audit(mission.Id, "MISSION_ASSIGNMENT_REMOVED");
        await SaveConcurrency(ct);
    }

    public async Task AssignDroneAsync(Guid missionId, Guid droneId, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct, true); RequirePreExecution(mission);
        if (droneId == Guid.Empty) throw new BusinessRuleException("DRONE_ASSIGNMENT_REQUIRED", "DroneId is required.");
        var (plannedStart, plannedEnd) = PlannedWindow(mission);

        Uav drone;
        if (plannedStart.HasValue && plannedEnd.HasValue)
        {
            // BR-20/BR-26: Available, Healthy or Warning, not Unsuitable for the scope and not booked in the window.
            drone = await MissionResourceGuard.EnsureDroneEligibleAsync(_db, droneId, plannedStart.Value, plannedEnd.Value, mission.Id, mission.PreMissionAssessmentId, ct);
        }
        else
        {
            drone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == droneId, ct) ?? throw new NotFoundException("Drone", droneId);
            if (drone.OperationalStatus != DroneOperationalStatus.Available)
                throw new BusinessRuleException("DRONE_NOT_AVAILABLE", $"UAV {drone.UavCode} is {drone.OperationalStatus}, not Available.");
            if (drone.TechnicalHealth == TechnicalHealth.Critical)
                throw new BusinessRuleException("DRONE_CRITICAL_HEALTH", $"UAV {drone.UavCode} has Critical technical health.");
            if (await _db.Missions.AnyAsync(x => x.Id != missionId && x.UavId == droneId && x.Status != MissionStatus.Completed && x.Status != MissionStatus.Cancelled, ct))
                throw new BusinessRuleException("DRONE_ALREADY_RESERVED");
        }
        if (drone.FlightStatus != DroneStatus.Idle && mission.UavId != droneId) throw new BusinessRuleException("DRONE_UNAVAILABLE", $"UAV {drone.UavCode} flight status is {drone.FlightStatus}, not Idle.");

        // One UAV per mission: cancel every Active UAV booking of the mission that is not for the new UAV (Table 60).
        await MissionResourceGuard.EndActiveBookingsAsync(_db, missionId, ResourceBookingStatus.Cancelled, ct, b => b.DroneId.HasValue && b.DroneId != droneId);
        if (plannedStart.HasValue && plannedEnd.HasValue
            && !await _db.ResourceBookings.AnyAsync(b => b.MissionId == missionId && b.DroneId == droneId && b.Status == ResourceBookingStatus.Active, ct))
        {
            _db.ResourceBookings.Add(new ResourceBooking
            {
                MissionId = missionId,
                DroneId = droneId,
                StartAt = plannedStart.Value,
                EndAt = plannedEnd.Value,
                Status = ResourceBookingStatus.Active
            });
        }

        var previousUavId = mission.UavId;
        var replaced = previousUavId.HasValue && previousUavId.Value != Guid.Empty && previousUavId != droneId;
        if (replaced)
        {
            // The old UAV goes back to the manager: close its open handovers in this mission (BR-36).
            foreach (var h in mission.DroneHandovers.Where(h => !h.IsDeleted && h.DroneId == previousUavId!.Value && h.ReturnedAt == null))
            {
                h.ReturnedAt = DateTime.UtcNow;
                if (h.Status == DroneHandoverStatus.Accepted) h.Status = DroneHandoverStatus.Returned;
            }
        }
        mission.UavId = droneId;
        mission.DroneCode = drone.UavCode;
        mission.RecalculateReadiness();
        TouchMission(mission);
        Audit(mission.Id, replaced ? "DRONE_REPLACED" : "DRONE_ASSIGNED"); await SaveConcurrency(ct);
    }

    public async Task<DroneHandover> ConfirmHandoverAsync(Guid missionId, Mf01Handover request, CancellationToken ct)
    {
        var mission = await AccessibleMission(missionId, ct, true);
        // BR-36: a member of the mission or a manager of its region records the handover.
        EnsureAssignedInspectorOrManager(mission);
        if (mission.Status is not (MissionStatus.Assigned or MissionStatus.Preparing)) throw new BusinessRuleException("HANDOVER_INVALID_STATE");
        if (mission.UavId != request.DroneId) throw new BusinessRuleException("DRONE_NOT_ASSIGNED");
        if (!mission.ActiveAssignments().Any(x => x.UserId == request.ReceivedBy)) throw new ForbiddenException("RECEIVER_NOT_ASSIGNED");
        if (string.IsNullOrWhiteSpace(request.Condition)) throw new BusinessRuleException("DRONE_CONDITION_REQUIRED");

        // One open (accepted, not returned) handover per UAV, across the missions that are still running.
        if (mission.DroneHandovers.Any(x => !x.IsDeleted && x.DroneId == request.DroneId && x.ReturnedAt == null && x.Status == DroneHandoverStatus.Accepted)
            || await OpenHandoverInOtherActiveMissionAsync(request.DroneId, mission.Id, ct))
            throw new BusinessRuleException("DUPLICATE_HANDOVER", "The UAV already has an open handover.");

        // Rejected handovers are closed at once (Table 60: Rejected → Returned); older rows may still be open.
        var staleRejected = mission.DroneHandovers.Where(x => !x.IsDeleted && x.DroneId == request.DroneId && x.ReturnedAt == null && x.Status == DroneHandoverStatus.Rejected).ToList();
        if (staleRejected.Count > 0)
        {
            foreach (var h in staleRejected) h.ReturnedAt = h.ReceivedAt;
            await SaveConcurrency(ct);
        }

        var now = DateTime.UtcNow;
        var handover = new DroneHandover { MissionId = missionId, DroneId = request.DroneId, HandedOverBy = _current.UserId,
            ReceivedBy = request.ReceivedBy, ReceivedAt = now, Condition = request.Condition,
            Status = request.Accepted ? DroneHandoverStatus.Accepted : DroneHandoverStatus.Rejected,
            ReturnedAt = request.Accepted ? null : now };
        _db.DroneHandovers.Add(handover); mission.DroneHandovers.Add(handover); mission.RecalculateReadiness(); TouchMission(mission);
        Audit(mission.Id, request.Accepted ? "DRONE_HANDOVER_CONFIRMED" : "DRONE_HANDOVER_REJECTED");
        await SaveConcurrency(ct); return handover;
    }

    public async Task<MissionCheckIn> CheckInAsync(Guid missionId, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await AccessibleMission(missionId, ct, true);
        if (mission.Status is not (MissionStatus.Assigned or MissionStatus.Preparing))
            throw new BusinessRuleException("CHECK_IN_INVALID_STATE");

        var assignment = mission.Assignments.SingleOrDefault(x => x.UserId == _current.UserId && x.Status == MissionAssignmentStatus.Active);
        if (assignment == null)
            throw new ForbiddenException("USER_NOT_ASSIGNED");

        if (assignment.ResponseStatus != MissionAssignmentResponse.Accepted)
            throw new BusinessRuleException("ASSIGNMENT_NOT_ACCEPTED");

        var previous = mission.CheckIns.FirstOrDefault(x => !x.IsDeleted && x.UserId == _current.UserId);
        if (previous?.Status == MissionCheckInStatus.CheckedIn)
            throw new BusinessRuleException("DUPLICATE_CHECK_IN");

        MissionCheckIn checkIn;
        if (previous != null)
        {
            // One check-in row per member and mission (unique index): a revoked one (member replaced and added back) is reopened.
            checkIn = previous;
            checkIn.Status = MissionCheckInStatus.CheckedIn;
            checkIn.CheckedInAt = DateTime.UtcNow;
        }
        else
        {
            checkIn = new MissionCheckIn { MissionId = missionId, UserId = _current.UserId, CheckedInAt = DateTime.UtcNow };
            _db.MissionCheckIns.Add(checkIn);
            mission.CheckIns.Add(checkIn);
        }
        var ready = mission.RecalculateReadiness();
        Audit(mission.Id, "CHECK_IN");
        if (ready) Audit(mission.Id, "MISSION_READY");
        await SaveConcurrency(ct);
        return checkIn;
    }

    public async Task<MissionAssignment> AcceptAssignmentAsync(Guid missionId, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await AccessibleMission(missionId, ct, true);
        var (assignment, changed, allConfirmed) = AcceptCurrentUser(mission);
        if (!changed) return assignment;
        await SaveConcurrency(ct);

        if (allConfirmed && _notifier != null)
        {
            var eventDto = new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = mission.Id.ToString(),
                Type = "CONFIRMED",
                Status = "CONFIRMED",
                ActorRole = assignment.AssignmentRole,
                ActorId = _current.UserId.ToString(),
                ActorName = _current.Username ?? "Thành viên",
                ManagerId = mission.ManagerId.ToString(),
                InspectorId = mission.PrimaryInspectorId()?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }

        return assignment;
    }

    public async Task<MissionAssignment> PostponeAssignmentAsync(Guid missionId, string reason, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("POSTPONE_REASON_REQUIRED", "A reason must be provided when postponing an assignment.");

        var mission = await AccessibleMission(missionId, ct, true);
        var assignment = mission.ActiveAssignments().SingleOrDefault(x => x.UserId == _current.UserId)
            ?? throw new NotFoundException("MissionAssignment", _current.UserId);
        RequireRespondableState(mission);

        assignment.ResponseStatus = MissionAssignmentResponse.Postponed;
        assignment.ResponseReason = reason;
        assignment.RespondedAt = DateTime.UtcNow;
        assignment.Version++;

        if (assignment.IsRequired)
        {
            // BR-30: a required member's postponement postpones the mission (with its reason).
            mission.Postpone(reason);
        }
        else
        {
            // An optional member only answers for themselves; the mission keeps its own postpone data.
            mission.RecalculateReadiness();
        }
        TouchMission(mission);

        Audit(mission.Id, "ASSIGNMENT_POSTPONED");
        NotifyOnce(new[] { mission.ManagerId }, mission, "ASSIGNMENT_POSTPONED");
        await SaveConcurrency(ct);
        return assignment;
    }

    public async Task StartAsync(Guid missionId, CancellationToken ct)
    {
        var m = await AccessibleMission(missionId, ct, true);
        EnsureAssignedInspectorOrManager(m);

        // BR-05/BR-38: Ready, Assigned or Preparing, and only once every required member has accepted (BR-30).
        if (m.Status is not (MissionStatus.Ready or MissionStatus.Assigned or MissionStatus.Preparing) || m.StartedAt != null)
            throw new BusinessRuleException("INVALID_MISSION_STATE", $"Cannot start mission with status {m.Status}.");
        if (!m.AllRequiredAccepted())
            throw new BusinessRuleException("REQUIRED_ACCEPTANCE_PENDING", "Every required member must accept before the start.");

        // BR-38 window: not earlier than the planned start minus the tolerance (Mission:StartToleranceMinutes).
        if (m.PlannedStart.HasValue)
        {
            var tolerance = await MissionResourceGuard.GetIntSettingAsync(_db, SettingKeys.MissionStartToleranceMinutes, MissionResourceGuard.DefaultStartToleranceMinutes, ct);
            var earliest = m.PlannedStart.Value.AddMinutes(-tolerance);
            if (DateTime.UtcNow < earliest)
                throw new BusinessRuleException("MISSION_START_TOO_EARLY", $"The mission can start from {earliest:yyyy-MM-dd HH:mm} UTC.");
        }

        // BR-12/BR-38: the UAV must still be usable and not flying another mission.
        if (!m.UavId.HasValue || m.UavId.Value == Guid.Empty)
            throw new BusinessRuleException("DRONE_ASSIGNMENT_REQUIRED", "The mission has no UAV.");
        var drone = await _db.Uavs.SingleOrDefaultAsync(d => d.Id == m.UavId.Value, ct)
            ?? throw new NotFoundException("Drone", m.UavId.Value);
        if (drone.OperationalStatus is not (DroneOperationalStatus.Available or DroneOperationalStatus.Reserved))
            throw new BusinessRuleException("DRONE_NOT_AVAILABLE", $"UAV {drone.UavCode} is {drone.OperationalStatus}.");
        if (drone.TechnicalHealth == TechnicalHealth.Critical)
            throw new BusinessRuleException("DRONE_CRITICAL_HEALTH", $"UAV {drone.UavCode} has Critical technical health.");
        if (await MissionResourceGuard.UavFlyingInOtherMissionAsync(_db, drone.Id, m.Id, ct))
            throw new BusinessRuleException("DRONE_IN_USE", $"UAV {drone.UavCode} is flying another mission.");
        if (drone.FlightStatus is not (DroneStatus.Idle or DroneStatus.Online))
            throw new BusinessRuleException("DRONE_IN_USE", $"UAV {drone.UavCode} flight status is {drone.FlightStatus}, not Idle.");
        if (await OpenHandoverInOtherActiveMissionAsync(drone.Id, m.Id, ct))
            throw new BusinessRuleException("DRONE_IN_USE", $"UAV {drone.UavCode} has an open handover in another mission.");

        var missionHandovers = m.DroneHandovers.Where(h => !h.IsDeleted && h.DroneId == drone.Id).ToList();
        var openHandover = missionHandovers.Any(h => h.Status == DroneHandoverStatus.Accepted && h.ReturnedAt == null);
        var latest = missionHandovers.OrderByDescending(h => h.ReceivedAt).FirstOrDefault();
        if (!openHandover && latest?.Status == DroneHandoverStatus.Rejected)
            throw new BusinessRuleException("HANDOVER_REJECTED", "The UAV was rejected at handover; change the UAV or record a new handover.");

        // A1 (UC-MF03-03): starting from Assigned/Preparing records the starter's check-in and an accepted handover.
        var now = DateTime.UtcNow;
        var starterAssignment = m.ActiveAssignments().FirstOrDefault(a => a.UserId == _current.UserId && a.ResponseStatus == MissionAssignmentResponse.Accepted);
        if (starterAssignment != null)
        {
            var checkIn = m.CheckIns.FirstOrDefault(c => !c.IsDeleted && c.UserId == _current.UserId);
            if (checkIn == null)
            {
                checkIn = new MissionCheckIn { Id = Guid.NewGuid(), MissionId = m.Id, UserId = _current.UserId, CheckedInAt = now, Status = MissionCheckInStatus.CheckedIn };
                _db.MissionCheckIns.Add(checkIn);
                m.CheckIns.Add(checkIn);
            }
            else if (checkIn.Status != MissionCheckInStatus.CheckedIn)
            {
                checkIn.Status = MissionCheckInStatus.CheckedIn;
                checkIn.CheckedInAt = now;
            }
        }

        if (!openHandover)
        {
            // Close stale open Rejected rows first: the unique index allows one unreturned handover per mission and UAV.
            var staleRejected = missionHandovers.Where(h => h.ReturnedAt == null).ToList();
            if (staleRejected.Count > 0)
            {
                foreach (var h in staleRejected) h.ReturnedAt = h.ReceivedAt;
                await SaveConcurrency(ct);
            }

            var receiverId = starterAssignment?.UserId ?? m.PrimaryInspectorId() ?? _current.UserId;
            var handover = new DroneHandover
            {
                Id = Guid.NewGuid(),
                MissionId = m.Id,
                DroneId = drone.Id,
                HandedOverBy = m.ManagerId != Guid.Empty ? m.ManagerId : _current.UserId,
                ReceivedBy = receiverId,
                ReceivedAt = now,
                Condition = "Pre-flight automated handover verification",
                Status = DroneHandoverStatus.Accepted
            };
            _db.DroneHandovers.Add(handover);
            m.DroneHandovers.Add(handover);
        }

        try
        {
            m.Start(now);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException("INVALID_MISSION_STATE", ex.Message);
        }

        // The UAV row is updated with its version: two missions starting the same UAV at once cannot both win.
        drone.FlightStatus = DroneStatus.Flying;
        drone.UpdatedAt = now;

        Audit(m.Id, "MISSION_STARTED");
        await SaveConcurrency(ct);

        if (_notifier != null)
        {
            var eventDto = new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = m.Id.ToString(),
                Type = "STARTED",
                Status = "IN_PROGRESS",
                ActorRole = "Inspector",
                ActorId = _current.UserId.ToString(),
                ActorName = _current.Username ?? "Phi công",
                ManagerId = m.ManagerId.ToString(),
                InspectorId = m.PrimaryInspectorId()?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }
    }

    public async Task CompleteAsync(Guid missionId, CancellationToken ct)
    {
        var m = await AccessibleMission(missionId, ct, true);
        EnsureAssignedInspectorOrManager(m);
        try
        {
            m.Complete();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException("INVALID_MISSION_STATE", ex.Message);
        }

        // BR-45: the UAV becomes Idle (unless another In Progress mission flies it) and every Active booking is released.
        await MissionResourceGuard.ReleaseUavAsync(_db, m.UavId, m.Id, ct);
        await MissionResourceGuard.EndActiveBookingsAsync(_db, m.Id, ResourceBookingStatus.Released, ct);

        Audit(m.Id, "MISSION_COMPLETED");
        await SaveConcurrency(ct);

        if (_notifier != null)
        {
            var eventDto = new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = m.Id.ToString(),
                Type = "COMPLETED",
                Status = "COMPLETED",
                ActorRole = "Inspector",
                ActorId = _current.UserId.ToString(),
                ActorName = _current.Username ?? "Phi công",
                ManagerId = m.ManagerId.ToString(),
                InspectorId = m.PrimaryInspectorId()?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }
    }

    [Obsolete("Use CancelMissionAsync instead.")]
    public Task CancelAsync(Guid missionId, CancellationToken ct) => CancelMissionAsync(missionId, null, ct);

    public async Task<Mission> ConfirmMissionAsync(Guid missionId, string? reason, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await AccessibleMission(missionId, ct, true);

        // /confirm is the caller's acceptance of their own assignment (BR-30): it never jumps the mission state.
        if (!mission.ActiveAssignments().Any(x => x.UserId == _current.UserId))
            throw new ForbiddenException("USER_NOT_ASSIGNED");
        var (_, changed, _) = AcceptCurrentUser(mission);
        if (!changed)
            return mission;

        var actorName = _current.Username ?? "Phi công";
        var commContent = string.IsNullOrWhiteSpace(reason)
            ? "Phi công đã xác nhận tiếp nhận sẵn sàng bay."
            : $"Phi công đã xác nhận tiếp nhận: {reason}";

        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            Type = "CONFIRM",
            Content = commContent,
            CreatedAt = DateTime.UtcNow
        };
        _db.MissionCommunicationLogs.Add(commLog);

        await SaveConcurrency(ct);

        if (_notifier != null)
        {
            var eventDto = new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = mission.Id.ToString(),
                Type = "CONFIRMED",
                Status = "CONFIRMED",
                ActorId = _current.UserId.ToString(),
                ActorName = actorName,
                ActorRole = "INSPECTOR",
                Reason = commContent,
                ManagerId = mission.ManagerId.ToString(),
                InspectorId = mission.PrimaryInspectorId()?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = new UavPms.Shared.Contracts.Events.MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = _current.UserId.ToString(),
                    SenderName = actorName,
                    SenderRole = "INSPECTOR",
                    Type = commLog.Type,
                    Content = commLog.Content,
                    Timestamp = commLog.CreatedAt
                }
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }

        return mission;
    }

    public async Task<Mission> SuspendMissionAsync(Guid missionId, string reason, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await ManagedMission(missionId, ct, true);

        try
        {
            mission.Suspend(reason);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException("INVALID_MISSION_STATE", ex.Message);
        }

        Audit(mission.Id, "MISSION_SUSPENDED");
        NotifyOnce(MemberIds(mission), mission, "MISSION_SUSPENDED");

        var actorName = _current.Username ?? "Quản lý";
        var commContent = string.IsNullOrWhiteSpace(reason)
            ? "Quản lý đã tạm đình chỉ bay khẩn cấp."
            : $"Quản lý đã tạm đình chỉ bay khẩn cấp: {reason}";

        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            Type = "SUSPEND",
            Content = commContent,
            CreatedAt = DateTime.UtcNow
        };
        _db.MissionCommunicationLogs.Add(commLog);

        await SaveConcurrency(ct);

        if (_notifier != null)
        {
            var eventDto = new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = mission.Id.ToString(),
                Type = "SUSPENDED",
                Status = "SUSPENDED",
                ActorId = _current.UserId.ToString(),
                ActorName = actorName,
                ActorRole = "MANAGER",
                Reason = reason,
                ManagerId = mission.ManagerId.ToString(),
                InspectorId = mission.PrimaryInspectorId()?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = new UavPms.Shared.Contracts.Events.MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = _current.UserId.ToString(),
                    SenderName = actorName,
                    SenderRole = "MANAGER",
                    Type = commLog.Type,
                    Content = commLog.Content,
                    Timestamp = commLog.CreatedAt
                }
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }

        return mission;
    }

    public async Task<Mission> ResumeMissionAsync(Guid missionId, string? reason, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await ManagedMission(missionId, ct, true);

        try
        {
            mission.Resume(reason);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException("INVALID_MISSION_STATE", ex.Message);
        }

        Audit(mission.Id, "MISSION_RESUMED");
        NotifyOnce(MemberIds(mission), mission, "MISSION_RESUMED");

        var actorName = _current.Username ?? "Quản lý";
        var commContent = string.IsNullOrWhiteSpace(reason)
            ? "Quản lý đã dỡ lệnh tạm đình chỉ bay."
            : $"Quản lý đã dỡ lệnh tạm đình chỉ bay: {reason}";

        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            Type = "RESUME",
            Content = commContent,
            CreatedAt = DateTime.UtcNow
        };
        _db.MissionCommunicationLogs.Add(commLog);

        await SaveConcurrency(ct);

        if (_notifier != null)
        {
            var eventDto = new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = mission.Id.ToString(),
                Type = "RESUMED",
                Status = mission.Status switch
                {
                    MissionStatus.InProgress => "IN_PROGRESS",
                    MissionStatus.PendingAcceptance => "PENDING_ACCEPTANCE",
                    _ => "CONFIRMED"
                },
                ActorId = _current.UserId.ToString(),
                ActorName = actorName,
                ActorRole = "MANAGER",
                Reason = commContent,
                ManagerId = mission.ManagerId.ToString(),
                InspectorId = mission.PrimaryInspectorId()?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = new UavPms.Shared.Contracts.Events.MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = _current.UserId.ToString(),
                    SenderName = actorName,
                    SenderRole = "MANAGER",
                    Type = commLog.Type,
                    Content = commLog.Content,
                    Timestamp = commLog.CreatedAt
                }
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }

        return mission;
    }

    public Task<MissionAssignment> ReassignAsync(Guid missionId, ReassignAssignmentRequest request, CancellationToken ct = default)
        => ReassignAsync(missionId, request.AssignmentId, request.NewUserId, request.Role, request.Reason, ct);

    public async Task<MissionAssignment> ReassignAsync(Guid missionId, Guid? assignmentId, Guid newUserId, string? role, string? reason, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await ManagedMission(missionId, ct, true);
        RequirePreExecution(mission);
        if (newUserId == Guid.Empty) throw new BusinessRuleException("ASSIGNEE_REQUIRED", "NewUserId is required.");

        // The assignment to replace: by id, or the active assignment with the given role. Never a silent new one.
        MissionAssignment oldAssignment;
        if (assignmentId.HasValue && assignmentId.Value != Guid.Empty)
        {
            oldAssignment = mission.ActiveAssignments().FirstOrDefault(x => x.Id == assignmentId.Value)
                ?? throw new NotFoundException("MissionAssignment", assignmentId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(role))
        {
            oldAssignment = mission.ActiveAssignments()
                .Where(x => string.Equals(x.AssignmentRole, role, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.AssignedAt)
                .FirstOrDefault()
                ?? throw new NotFoundException($"No active {role} assignment in mission {missionId}.");
        }
        else
        {
            throw new BusinessRuleException("ASSIGNMENT_ID_REQUIRED", "AssignmentId (or Role) is required.");
        }

        if (mission.ActiveAssignments().Any(x => x.UserId == newUserId))
            throw new BusinessRuleException("ASSIGNEE_ALREADY_ASSIGNED", $"User {newUserId} is already a member of this mission.");

        // BR-18/BR-19/BR-32: the new user is Active, eligible and not booked in the window.
        var (plannedStart, plannedEnd) = PlannedWindow(mission);
        if (plannedStart.HasValue && plannedEnd.HasValue)
        {
            await MissionResourceGuard.EnsurePersonEligibleAsync(_db, newUserId, RequireRegion(mission), plannedStart.Value, plannedEnd.Value, mission.Id, IsGlobal, ct);
        }
        else
        {
            var newUser = await _db.Users.SingleOrDefaultAsync(x => x.Id == newUserId, ct) ?? throw new NotFoundException("User", newUserId);
            if (!IsActive(newUser.Status)) throw new BusinessRuleException("ASSIGNEE_INACTIVE");
        }

        var oldUserId = oldAssignment.UserId;
        var wasPrimary = mission.PrimaryInspectorId() == oldUserId;
        var effectiveRole = !string.IsNullOrWhiteSpace(role) ? role.Trim() : oldAssignment.AssignmentRole;

        // Table 60: the replaced member's assignment becomes Replaced (record Revoked) and their booking Released.
        oldAssignment.Status = MissionAssignmentStatus.Revoked;
        oldAssignment.ResponseStatus = MissionAssignmentResponse.Replaced;
        oldAssignment.ResponseReason = string.IsNullOrWhiteSpace(reason) ? oldAssignment.ResponseReason : reason;
        oldAssignment.EndedAt = DateTime.UtcNow;
        oldAssignment.Version++;
        await MissionResourceGuard.EndActiveBookingsAsync(_db, missionId, ResourceBookingStatus.Released, ct, b => b.UserId == oldUserId);
        CloseMemberFieldRecords(mission, oldUserId);

        var targetAssignment = new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = newUserId,
            AssignmentRole = effectiveRole,
            AssignedById = _current.UserId,
            IsRequired = oldAssignment.IsRequired,
            ResponseStatus = MissionAssignmentResponse.Pending,
            ResponseReason = reason
        };
        _db.MissionAssignments.Add(targetAssignment);
        mission.Assignments.Add(targetAssignment);

        if (IsInspectorRole(effectiveRole) || wasPrimary)
        {
            mission.AssignedToUserId = newUserId;
        }

        if (plannedStart.HasValue && plannedEnd.HasValue)
        {
            _db.ResourceBookings.Add(new ResourceBooking
            {
                MissionId = missionId,
                UserId = newUserId,
                StartAt = plannedStart.Value,
                EndAt = plannedEnd.Value,
                Status = ResourceBookingStatus.Active
            });
        }

        // BR-32: a new required member who has not accepted returns the mission to Pending Acceptance
        // (a suspended mission re-checks this on resume).
        mission.RecalculateReadiness();
        TouchMission(mission);

        NotifyOnce(new[] { newUserId }, mission, "MISSION_ASSIGNED", effectiveRole);
        NotifyOnce(new[] { oldUserId }, mission, "MISSION_ASSIGNMENT_REMOVED", oldAssignment.AssignmentRole);
        Audit(mission.Id, "ASSIGNMENT_REASSIGNED");
        await SaveConcurrency(ct);

        if (_notifier != null)
        {
            await _notifier.NotifyAsync(new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = mission.Id.ToString(),
                MissionCode = mission.MissionCode,
                MissionTitle = mission.Title,
                Type = "REASSIGNED",
                Status = mission.Status.ToString(),
                TargetUserId = targetAssignment.UserId.ToString(),
                ActorRole = "MANAGER",
                ActorId = _current.UserId.ToString(),
                ActorName = _current.Username ?? "Manager",
                Reason = reason,
                Timestamp = DateTime.UtcNow
            }, ct);
        }

        return targetAssignment;
    }

    public async Task<Mission> PostponeMissionAsync(Guid missionId, string reason, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("POSTPONE_REASON_REQUIRED", "A reason must be provided when postponing an assignment.");

        // UC-MF02-09: the mission-level postpone is a manager action of the region (BR-02); a member postpones
        // their own assignment through /assignments/postpone (BR-30).
        var mission = await ManagedMission(missionId, ct, true);

        try
        {
            mission.Postpone(reason);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException("INVALID_MISSION_STATE", ex.Message);
        }

        Audit(mission.Id, "MISSION_POSTPONED");
        NotifyOnce(MemberIds(mission), mission, "MISSION_POSTPONED");

        var actorName = _current.Username ?? "Quản lý";
        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            Type = "POSTPONE",
            Content = $"Quản lý đã hoãn nhiệm vụ: {reason}",
            CreatedAt = DateTime.UtcNow
        };
        _db.MissionCommunicationLogs.Add(commLog);

        await SaveConcurrency(ct);

        if (_notifier != null)
        {
            var eventDto = new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = mission.Id.ToString(),
                Type = "POSTPONED",
                Status = "POSTPONED",
                ActorId = _current.UserId.ToString(),
                ActorName = actorName,
                ActorRole = "MANAGER",
                Reason = reason,
                ManagerId = mission.ManagerId.ToString(),
                InspectorId = mission.PrimaryInspectorId()?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = new UavPms.Shared.Contracts.Events.MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = _current.UserId.ToString(),
                    SenderName = actorName,
                    SenderRole = "MANAGER",
                    Type = commLog.Type,
                    Content = commLog.Content,
                    Timestamp = commLog.CreatedAt
                }
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }

        return mission;
    }

    public async Task<Mission> CancelMissionAsync(Guid missionId, string? reason, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct, true);
        try
        {
            mission.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException("INVALID_MISSION_STATE", ex.Message);
        }

        // BR-33: bookings are cancelled. A suspended mission that had started also gives its UAV back: open
        // handovers are closed and a Flying UAV becomes Idle unless another In Progress mission flies it.
        await MissionResourceGuard.EndActiveBookingsAsync(_db, missionId, ResourceBookingStatus.Cancelled, ct);
        var now = DateTime.UtcNow;
        foreach (var h in mission.DroneHandovers.Where(h => !h.IsDeleted && h.ReturnedAt == null))
        {
            h.ReturnedAt = h.Status == DroneHandoverStatus.Rejected ? h.ReceivedAt : now;
            if (h.Status == DroneHandoverStatus.Accepted) h.Status = DroneHandoverStatus.Returned;
        }
        await MissionResourceGuard.ReleaseUavAsync(_db, mission.UavId, mission.Id, ct);

        if (mission.PreMissionAssessmentId.HasValue)
        {
            var assessment = await _db.PreMissionAssessments.FirstOrDefaultAsync(x => x.Id == mission.PreMissionAssessmentId.Value, ct);
            if (assessment != null)
            {
                // Giải phóng đánh giá để có thể tạo nhiệm vụ khác từ nó (liên kết một chiều Missions.PreMissionAssessmentId).
                mission.PreMissionAssessmentId = null;
                if (assessment.ValidUntil > DateTime.UtcNow)
                {
                    assessment.Status = PreMissionAssessmentStatus.Ready;
                }
                else
                {
                    assessment.Status = PreMissionAssessmentStatus.Expired;
                }
                assessment.Version++;
            }
        }

        Audit(mission.Id, "MISSION_CANCELLED");
        NotifyOnce(MemberIds(mission), mission, "MISSION_CANCELLED");

        var actorName = _current.Username ?? "Quản lý";
        var commContent = string.IsNullOrWhiteSpace(reason)
            ? "Quản lý đã hủy bỏ nhiệm vụ."
            : $"Quản lý đã hủy bỏ nhiệm vụ: {reason}";

        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            Type = "CANCEL",
            Content = commContent,
            CreatedAt = DateTime.UtcNow
        };
        _db.MissionCommunicationLogs.Add(commLog);

        await SaveConcurrency(ct);

        if (_notifier != null)
        {
            var eventDto = new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = mission.Id.ToString(),
                Type = "CANCELLED",
                Status = "Cancelled",
                ActorId = _current.UserId.ToString(),
                ActorName = actorName,
                ActorRole = "MANAGER",
                Reason = reason,
                ManagerId = mission.ManagerId.ToString(),
                InspectorId = mission.PrimaryInspectorId()?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = new UavPms.Shared.Contracts.Events.MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = _current.UserId.ToString(),
                    SenderName = actorName,
                    SenderRole = "MANAGER",
                    Type = commLog.Type,
                    Content = commLog.Content,
                    Timestamp = commLog.CreatedAt
                }
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }

        return mission;
    }

    public async Task RemindMissionAsync(Guid missionId, string? reason, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await ManagedMission(missionId, ct, true);

        Audit(mission.Id, "MISSION_REMINDER");
        var inspectorId = (mission.PrimaryInspectorId().HasValue && mission.PrimaryInspectorId().Value != Guid.Empty)
            ? mission.PrimaryInspectorId().Value
            : (mission.AssignedToUserId != Guid.Empty
                ? mission.AssignedToUserId
                : (mission.Assignments.FirstOrDefault(a => a.Status == MissionAssignmentStatus.Active)?.UserId ?? Guid.Empty));
        if (inspectorId != Guid.Empty)
        {
            Notify(inspectorId, mission, "MISSION_REMINDER");
        }

        var actorName = _current.Username ?? "Quản lý";
        var commContent = string.IsNullOrWhiteSpace(reason)
            ? "Nhắc nhở khẩn cấp: Vui lòng kiểm tra và tiếp nhận nhiệm vụ."
            : $"Nhắc nhở khẩn cấp: {reason}";

        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            Type = "REMINDER",
            Content = commContent,
            CreatedAt = DateTime.UtcNow
        };
        _db.MissionCommunicationLogs.Add(commLog);

        await SaveConcurrency(ct);

        if (_notifier != null)
        {
            var eventDto = new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = mission.Id.ToString(),
                Type = "REMINDER",
                ActorId = _current.UserId.ToString(),
                ActorName = actorName,
                ActorRole = "MANAGER",
                Reason = reason,
                TargetUserId = inspectorId != Guid.Empty ? inspectorId.ToString() : null,
                InspectorId = inspectorId != Guid.Empty ? inspectorId.ToString() : null,
                ManagerId = mission.ManagerId.ToString(),
                Timestamp = DateTime.UtcNow,
                Log = new UavPms.Shared.Contracts.Events.MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = _current.UserId.ToString(),
                    SenderName = actorName,
                    SenderRole = "MANAGER",
                    Type = commLog.Type,
                    Content = commLog.Content,
                    Timestamp = commLog.CreatedAt
                }
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }
    }

    public async Task<MissionCommunicationLogDto> AddCommunicationAsync(Guid missionId, string message, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        if (string.IsNullOrWhiteSpace(message))
            throw new BusinessRuleException("MESSAGE_REQUIRED", "Tin nhắn không được để trống.");

        var mission = await AccessibleMission(missionId, ct, true);

        var isManager = _current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase)
                     || _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase);
        var senderRole = isManager ? "MANAGER" : "INSPECTOR";
        var actorName = _current.Username ?? (isManager ? "Quản lý" : "Phi công");

        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            Type = "MESSAGE",
            Content = message,
            CreatedAt = DateTime.UtcNow
        };
        _db.MissionCommunicationLogs.Add(commLog);

        var recipientId = isManager
            ? ((mission.PrimaryInspectorId().HasValue && mission.PrimaryInspectorId().Value != Guid.Empty) ? mission.PrimaryInspectorId().Value : mission.AssignedToUserId)
            : mission.ManagerId;
        if (recipientId != Guid.Empty)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = recipientId,
                Type = "MISSION_COMMUNICATION",
                ReferenceType = "Mission",
                ReferenceId = mission.Id,
                Title = $"[MF02] Tin nhắn mới trong nhiệm vụ {mission.MissionCode}",
                Body = $"{actorName}: {message}"
            });
        }

        await SaveConcurrency(ct);

        var logDto = new MissionCommunicationLogDto
        {
            Id = commLog.Id.ToString(),
            SenderId = _current.UserId.ToString(),
            SenderName = actorName,
            SenderRole = senderRole,
            Type = commLog.Type,
            Content = commLog.Content,
            Timestamp = commLog.CreatedAt
        };

        if (_notifier != null)
        {
            var eventDto = new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = mission.Id.ToString(),
                Type = "COMMUNICATION",
                ActorId = _current.UserId.ToString(),
                ActorName = actorName,
                ActorRole = senderRole,
                Message = message,
                ManagerId = mission.ManagerId.ToString(),
                InspectorId = mission.PrimaryInspectorId()?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = logDto
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }

        return logDto;
    }

    public async Task<IReadOnlyList<MissionCommunicationLogDto>> GetCommunicationsAsync(Guid missionId, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        await AccessibleMission(missionId, ct, false);

        var logs = await _db.MissionCommunicationLogs
            .Where(x => x.MissionId == missionId && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new MissionCommunicationLogDto
            {
                Id = x.Id.ToString(),
                SenderId = x.SenderId.HasValue ? x.SenderId.Value.ToString() : string.Empty,
                SenderName = x.Sender != null ? x.Sender.FullName : "Hệ thống",
                SenderRole = x.SenderId == null ? "SYSTEM" : (x.SenderId == x.Mission!.ManagerId ? "MANAGER" : SenderRoleOf(
                    x.Mission.Assignments.Where(a => a.UserId == x.SenderId).Select(a => a.AssignmentRole).FirstOrDefault(),
                    x.Sender!.UserRoles.Select(ur => ur.Role!.RoleName).FirstOrDefault())),
                Type = x.Type,
                Content = x.Content,
                Timestamp = x.CreatedAt
            })
            .ToListAsync(ct);

        return logs;
    }

    public async Task<IReadOnlyList<MissionDetectionDto>> GetMissionDetectionsAsync(
        Guid missionId,
        string? status,
        string? mediaType,
        bool? isEmergency,
        CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        await AccessibleMission(missionId, ct, false);

        var mediaQuery = _db.InspectionMedia.Where(m => m.MissionId == missionId && !m.IsDeleted);
        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            var mt = mediaType.Trim().ToLowerInvariant();
            mediaQuery = mediaQuery.Where(m => m.MediaType.ToLower() == mt);
        }

        var mediaList = await mediaQuery.ToListAsync(ct);
        if (mediaList.Count == 0)
        {
            return Array.Empty<MissionDetectionDto>();
        }

        var mediaIds = mediaList.Select(m => m.Id).ToList();
        var mediaMap = mediaList.ToDictionary(m => m.Id);

        var anomaliesQuery = _db.DetectedAnomalies
            .Include(a => a.Category)
            .Include(a => a.Tower)
            .Include(a => a.EmergencyAlerts)
            .Where(a => mediaIds.Contains(a.MediaId) && !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToLowerInvariant();
            if (s is "approved" or "confirmed")
            {
                anomaliesQuery = anomaliesQuery.Where(a => a.ValidationStatus == "Confirmed" || a.ValidationStatus == "Approved");
            }
            else if (s == "rejected")
            {
                anomaliesQuery = anomaliesQuery.Where(a => a.ValidationStatus == "Rejected");
            }
            else if (s == "pending")
            {
                anomaliesQuery = anomaliesQuery.Where(a => a.ValidationStatus != "Confirmed" && a.ValidationStatus != "Approved" && a.ValidationStatus != "Rejected");
            }
        }

        if (isEmergency.HasValue)
        {
            anomaliesQuery = anomaliesQuery.Where(a => (a.Category != null && a.Category.IsEmergencyClass == isEmergency.Value) || a.EmergencyAlerts.Any() == isEmergency.Value);
        }

        var anomalies = await anomaliesQuery.OrderByDescending(a => a.CreatedAt).ToListAsync(ct);

        var results = new List<MissionDetectionDto>(anomalies.Count);
        foreach (var a in anomalies)
        {
            mediaMap.TryGetValue(a.MediaId, out var media);
            var isEmerg = (a.Category != null && a.Category.IsEmergencyClass) || a.EmergencyAlerts.Any();
            var detStatus = string.Equals(a.ValidationStatus, "Confirmed", StringComparison.OrdinalIgnoreCase) || string.Equals(a.ValidationStatus, "Approved", StringComparison.OrdinalIgnoreCase)
                ? "Approved"
                : (string.Equals(a.ValidationStatus, "Rejected", StringComparison.OrdinalIgnoreCase) ? "Rejected" : "Pending");

            results.Add(new MissionDetectionDto
            {
                Id = a.Id.ToString(),
                MissionId = missionId.ToString(),
                MediaId = a.MediaId.ToString(),
                Title = !string.IsNullOrWhiteSpace(a.Category?.CategoryName) ? a.Category.CategoryName : "Khuyết tật thiết bị",
                Confidence = a.ConfidenceScore <= 1.0 ? Math.Round(a.ConfidenceScore * 100, 1) : Math.Round(a.ConfidenceScore, 1),
                CategoryCode = a.Category?.CategoryCode ?? "DEF-UNKNOWN",
                SeverityWeight = a.Category?.SeverityWeight ?? 1,
                IsEmergency = isEmerg,
                Status = detStatus,
                BoundingBox = ParseBoundingBoxDto(a.BoundingBox),
                TimestampSeconds = a.FrameTimeSeconds,
                TimestampLabel = a.FrameTimeSeconds.HasValue ? TimeSpan.FromSeconds(a.FrameTimeSeconds.Value).ToString(@"mm\:ss") : null,
                FrameIndex = a.FrameIndex,
                ImageUrl = !string.IsNullOrWhiteSpace(a.ImageUrl) ? a.ImageUrl : (!string.IsNullOrWhiteSpace(a.CropUrl) ? a.CropUrl : media?.FileUrl),
                SourceUrl = media?.FileUrl,
                TowerId = a.TowerId?.ToString(),
                Tower = a.Tower?.TowerCode ?? "Cột chưa xác định",
                Gps = a.Location == null ? null : JsonSerializer.Serialize(new { lat = a.Location.Y, lng = a.Location.X }),
                Description = !string.IsNullOrWhiteSpace(a.Category?.Description) ? a.Category.Description : a.AnalystNotes,
                DetectedAt = a.CreatedAt,
                ReviewedByUserId = a.AnalystId?.ToString(),
                ReviewedAt = a.ValidatedAt,
                ReviewNotes = a.AnalystNotes
            });
        }

        return results;
    }

    public async Task<ReviewDetectionResultDto> ReviewDetectionAsync(
        Guid missionId,
        Guid detectionId,
        ReviewDetectionRequest request,
        CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await AccessibleMission(missionId, ct, false);

        var anomaly = await _db.DetectedAnomalies
            .Include(a => a.Category)
            .Include(a => a.Media)
            .SingleOrDefaultAsync(a => a.Id == detectionId && !a.IsDeleted, ct)
            ?? throw new NotFoundException("DetectedAnomaly", detectionId);

        if (anomaly.Media == null || anomaly.Media.MissionId != missionId)
        {
            throw new BusinessRuleException("ANOMALY_MISSION_MISMATCH", $"Khuyết tật '{detectionId}' không thuộc nhiệm vụ '{missionId}'.");
        }

        var isApproved = string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(request.Status, "Confirmed", StringComparison.OrdinalIgnoreCase);
        var isRejected = string.Equals(request.Status, "Rejected", StringComparison.OrdinalIgnoreCase);

        if (!isApproved && !isRejected)
        {
            throw new BusinessRuleException("INVALID_REVIEW_STATUS", "Trạng thái phê duyệt phải là 'Approved' hoặc 'Rejected'.");
        }

        if (isApproved && !anomaly.TowerId.HasValue)
        {
            // Bất thường phải gắn cột điện trước khi xác nhận (CK_DetectedAnomalies_ConfirmedRequiresTower).
            anomaly.TowerId = anomaly.Media.TowerId
                ?? throw new BusinessRuleException("TOWER_REQUIRED", "Khuyết tật phải được gắn với cột điện trước khi xác nhận.");
        }

        anomaly.ValidationStatus = isApproved ? "Confirmed" : "Rejected";
        anomaly.AnalystId = _current.UserId;
        anomaly.AnalystNotes = request.ReviewNotes ?? string.Empty;
        anomaly.ValidatedAt = DateTime.UtcNow;

        string? createdTaskId = null;
        double? newHealthScore = null; // Tính năng điểm sức khỏe đã bỏ (ERD v2.2); giữ trường null cho tương thích API.

        if (isApproved)
        {
            // 2. Tự động sinh phiếu bảo dưỡng (MaintenanceTask) nếu chưa tồn tại
            var existingTicket = await _db.MaintenanceTickets
                .SingleOrDefaultAsync(t => t.AnomalyId == anomaly.Id && !t.IsDeleted, ct);

            if (existingTicket == null)
            {
                var priority = TicketPriority.High;
                if (string.Equals(request.OverrideSeverity, "Critical Risk", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(request.OverrideSeverity, "Urgent", StringComparison.OrdinalIgnoreCase) ||
                    (anomaly.Category != null && anomaly.Category.IsEmergencyClass) ||
                    (anomaly.Category != null && anomaly.Category.SeverityWeight >= 4))
                {
                    priority = TicketPriority.Emergency;
                }
                else if (string.Equals(request.OverrideSeverity, "Low", StringComparison.OrdinalIgnoreCase))
                {
                    priority = TicketPriority.Low;
                }

                var techAssignment = await _db.MissionAssignments
                    .FirstOrDefaultAsync(a => a.MissionId == missionId && a.AssignmentRole.ToUpper() == "TECHNICIAN" && a.Status == MissionAssignmentStatus.Active, ct);

                var ticket = new MaintenanceTicket
                {
                    Id = Guid.NewGuid(),
                    TicketCode = $"TKT-{DateTime.UtcNow:yyyyMMdd}-{RandomNumberGenerator.GetInt32(1000, 10000)}",
                    AnomalyId = anomaly.Id,
                    ManagerId = mission.ManagerId,
                    TechnicianId = techAssignment?.UserId,
                    Status = TicketStatus.Open,
                    Priority = priority,
                    Description = $"[Khuyến nghị từ Giám định AI] {anomaly.Category?.CategoryName ?? "Khuyết tật đường dây"}. {request.ReviewNotes ?? anomaly.AnalystNotes}",
                    AssignedAt = techAssignment != null ? DateTime.UtcNow : null,
                    DueDate = DateTime.UtcNow.AddDays(priority == TicketPriority.Emergency ? 1 : 7)
                };

                _db.MaintenanceTickets.Add(ticket);
                createdTaskId = ticket.Id.ToString();
            }
            else
            {
                createdTaskId = existingTicket.Id.ToString();
            }
        }

        Audit(mission.Id, isApproved ? "DETECTION_APPROVED" : "DETECTION_REJECTED");
        await _db.SaveChangesAsync(ct);

        if (_notifier != null)
        {
            try
            {
                await _notifier.NotifyAsync(new MissionLifecycleEventDto
                {
                    MissionId = missionId.ToString(),
                    Type = "DETECTION_REVIEWED",
                    Status = isApproved ? "Approved" : "Rejected",
                    Reason = request.ReviewNotes ?? anomaly.AnalystNotes,
                    Timestamp = anomaly.ValidatedAt ?? DateTime.UtcNow
                }, ct);

                if (createdTaskId != null)
                {
                    await _notifier.NotifyAsync(new MissionLifecycleEventDto
                    {
                        MissionId = missionId.ToString(),
                        Type = "MAINTENANCE_TASK_CREATED",
                        Status = "Open",
                        Reason = $"Phiếu bảo trì {createdTaskId} được tạo cho khuyết tật {anomaly.Id}",
                        Timestamp = DateTime.UtcNow
                    }, ct);
                }
            }
            catch
            {
                // Non-blocking realtime notification
            }
        }

        return new ReviewDetectionResultDto
        {
            DetectionId = anomaly.Id.ToString(),
            MissionId = missionId.ToString(),
            Status = isApproved ? "Approved" : "Rejected",
            ReviewNotes = anomaly.AnalystNotes,
            MaintenanceTaskId = createdTaskId,
            NewAssetHealthScore = newHealthScore,
            ReviewedAt = anomaly.ValidatedAt ?? DateTime.UtcNow
        };
    }

    public async Task<IReadOnlyList<MissionMaintenanceTaskDto>> GetMissionMaintenanceTasksAsync(
        Guid missionId,
        CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        await AccessibleMission(missionId, ct, false);

        var mediaIds = await _db.InspectionMedia
            .Where(m => m.MissionId == missionId && !m.IsDeleted)
            .Select(m => m.Id)
            .ToListAsync(ct);

        if (mediaIds.Count == 0)
        {
            return Array.Empty<MissionMaintenanceTaskDto>();
        }

        var anomalyIds = await _db.DetectedAnomalies
            .Where(a => mediaIds.Contains(a.MediaId) && (a.ValidationStatus == "Confirmed" || a.ValidationStatus == "Approved") && !a.IsDeleted)
            .Select(a => a.Id)
            .ToListAsync(ct);

        if (anomalyIds.Count == 0)
        {
            return Array.Empty<MissionMaintenanceTaskDto>();
        }

        var tickets = await _db.MaintenanceTickets
            .Include(t => t.Anomaly)
                .ThenInclude(a => a!.Category)
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Tower)
            .Where(t => anomalyIds.Contains(t.AnomalyId) && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var results = new List<MissionMaintenanceTaskDto>(tickets.Count);
        foreach (var t in tickets)
        {
            var priorityStr = t.Priority switch
            {
                TicketPriority.Emergency => "Urgent",
                TicketPriority.High => "High",
                TicketPriority.Medium => "Medium",
                _ => "Low"
            };

            var statusStr = t.Status switch
            {
                TicketStatus.Open => "Pending",
                TicketStatus.InProgress => "InProgress",
                TicketStatus.PendingVerification => "PendingVerification",
                TicketStatus.Resolved or TicketStatus.Closed => "Completed",
                _ => "Pending"
            };

            var catName = t.Anomaly?.Category?.CategoryName ?? "Khuyết tật thiết bị";
            var towerCode = t.Anomaly?.Tower?.TowerCode ?? "Cột chưa xác định";

            results.Add(new MissionMaintenanceTaskDto
            {
                Id = t.Id.ToString(),
                MissionId = missionId.ToString(),
                DetectionId = t.AnomalyId.ToString(),
                Title = $"Khắc phục & sửa chữa {catName}",
                Priority = priorityStr,
                TowerCode = towerCode,
                DefectDescription = t.Description,
                SuggestedAction = $"Cắt điện xuất tuyến, chuẩn bị vật tư và nhân lực chuyên dụng để xử lý {catName} theo quy trình kỹ thuật an toàn.",
                Status = statusStr,
                AssignedTeam = "Đội Truyền tải Điện Hà Nội 1",
                CreatedAt = t.CreatedAt
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<MissionActivityDto>> GetActivitiesAsync(Guid missionId, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        await AccessibleMission(missionId, ct, false);

        var logs = await _db.MissionCommunicationLogs
            .Where(x => x.MissionId == missionId && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new MissionActivityDto
            {
                Id = x.Id.ToString(),
                MissionId = x.MissionId.ToString(),
                SenderUserId = x.SenderId.HasValue ? x.SenderId.Value.ToString() : string.Empty,
                SenderName = x.Sender != null ? x.Sender.FullName : "Hệ thống",
                SenderRole = x.SenderId == null ? "SYSTEM" : (x.SenderId == x.Mission!.ManagerId ? "MANAGER" : SenderRoleOf(
                    x.Mission.Assignments.Where(a => a.UserId == x.SenderId).Select(a => a.AssignmentRole).FirstOrDefault(),
                    x.Sender!.UserRoles.Select(ur => ur.Role!.RoleName).FirstOrDefault())),
                Content = x.Content,
                Timestamp = x.CreatedAt
            })
            .ToListAsync(ct);

        return logs;
    }

    /// <summary>Role badge of a non-manager message sender: their assignment role on the mission, else their account role.</summary>
    internal static string SenderRoleOf(string? assignmentRole, string? accountRole)
    {
        if (!string.IsNullOrWhiteSpace(assignmentRole)) return assignmentRole.Trim().ToUpperInvariant();
        return (accountRole ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "analyst" => "ANALYST",
            "technician" or "maintenancetechnician" => "TECHNICIAN",
            "manager" or "systemadmin" => "MANAGER",
            _ => "INSPECTOR"
        };
    }

    public async Task<MissionActivityDto> AddActivityAsync(
        Guid missionId,
        CreateMissionActivityRequest request,
        CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await AccessibleMission(missionId, ct, false);

        var content = !string.IsNullOrWhiteSpace(request.Content) ? request.Content : request.Message;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new BusinessRuleException("ACTIVITY_CONTENT_REQUIRED", "Nội dung trao đổi không được để trống.");
        }

        var actorName = _current.Username ?? "Thành viên";
        var userAssignment = mission.Assignments.FirstOrDefault(a => a.UserId == _current.UserId && a.Status == MissionAssignmentStatus.Active);

        // Resolve actual valid role for caller
        string actualRole;
        if (mission.ManagerId == _current.UserId || _current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase))
        {
            actualRole = "MANAGER";
        }
        else if (userAssignment != null && !string.IsNullOrWhiteSpace(userAssignment.AssignmentRole))
        {
            actualRole = userAssignment.AssignmentRole.ToUpperInvariant();
        }
        else if (_current.Roles.Contains(UserRoles.Inspector, StringComparer.OrdinalIgnoreCase))
        {
            actualRole = "INSPECTOR";
        }
        else if (_current.Roles.Contains(UserRoles.Analyst, StringComparer.OrdinalIgnoreCase))
        {
            actualRole = "ANALYST";
        }
        else if (_current.Roles.Contains(UserRoles.Technician, StringComparer.OrdinalIgnoreCase) ||
                 _current.Roles.Contains(UserRoles.MaintenanceTechnician, StringComparer.OrdinalIgnoreCase))
        {
            actualRole = "TECHNICIAN";
        }
        else
        {
            actualRole = "MEMBER";
        }

        // Only allow client-specified SenderRole if user actually holds that role
        var senderRole = actualRole;
        if (!string.IsNullOrWhiteSpace(request.SenderRole))
        {
            var requestedRole = request.SenderRole.Trim().ToUpperInvariant();
            if (string.Equals(requestedRole, actualRole, StringComparison.OrdinalIgnoreCase) ||
                _current.Roles.Contains(requestedRole, StringComparer.OrdinalIgnoreCase) ||
                (userAssignment != null && string.Equals(userAssignment.AssignmentRole, requestedRole, StringComparison.OrdinalIgnoreCase)))
            {
                senderRole = requestedRole;
            }
        }

        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            Type = "MESSAGE",
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _db.MissionCommunicationLogs.Add(commLog);
        await _db.SaveChangesAsync(ct);

        var dto = new MissionActivityDto
        {
            Id = commLog.Id.ToString(),
            MissionId = mission.Id.ToString(),
            SenderUserId = (commLog.SenderId ?? Guid.Empty).ToString(),
            SenderName = actorName,
            SenderRole = senderRole,
            Content = commLog.Content,
            Timestamp = commLog.CreatedAt
        };

        if (_notifier != null)
        {
            var eventDto = new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = mission.Id.ToString(),
                Type = "COMMUNICATION",
                ActorId = _current.UserId.ToString(),
                ActorName = actorName,
                ActorRole = senderRole,
                Message = content,
                ManagerId = mission.ManagerId.ToString(),
                InspectorId = mission.PrimaryInspectorId()?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = new MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = (commLog.SenderId ?? Guid.Empty).ToString(),
                    SenderName = actorName,
                    SenderRole = senderRole,
                    Type = commLog.Type,
                    Content = commLog.Content,
                    Timestamp = commLog.CreatedAt
                }
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }

        return dto;
    }

    public async Task<MissionAssignmentsOverviewDto> GetAssignmentsOverviewAsync(Guid missionId, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await AccessibleMission(missionId, ct, false);

        var activeAssignments = mission.Assignments
            .Where(a => a.Status == MissionAssignmentStatus.Active && !a.IsDeleted)
            .ToList();

        var requiredList = activeAssignments.Where(a => a.IsRequired).ToList();
        var totalRequired = requiredList.Count > 0 ? requiredList.Count : activeAssignments.Count;
        var confirmedCount = requiredList.Count > 0
            ? requiredList.Count(a => a.ResponseStatus == MissionAssignmentResponse.Accepted)
            : activeAssignments.Count(a => a.ResponseStatus == MissionAssignmentResponse.Accepted);

        var allConfirmed = totalRequired > 0 && confirmedCount >= totalRequired;

        var items = activeAssignments.Select(a => new MissionAssignmentItemDto
        {
            Id = a.Id.ToString(),
            UserId = a.UserId.ToString(),
            UserName = a.User?.FullName ?? a.UserId.ToString(),
            UserFullName = a.User?.FullName ?? "Chưa rõ",
            AssignmentRole = a.AssignmentRole,
            Status = a.Status.ToString(),
            ResponseStatus = a.ResponseStatus.ToString(),
            IsRequired = a.IsRequired,
            AssignedAt = a.AssignedAt,
            RespondedAt = a.RespondedAt,
            ResponseReason = a.ResponseReason
        }).ToList();

        return new MissionAssignmentsOverviewDto
        {
            MissionId = mission.Id.ToString(),
            TotalRequiredCount = totalRequired,
            ConfirmedCount = confirmedCount,
            AllConfirmed = allConfirmed,
            ConfirmationDeadline = mission.ConfirmationDeadline,
            Assignments = items
        };
    }

    public async Task<DroneHandover> ReturnDroneHandoverAsync(Guid missionId, Guid droneId, string condition, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await AccessibleMission(missionId, ct, true);
        EnsureAssignedInspectorOrManager(mission);
        if (mission.Status is not (MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.InProgress))
            throw new BusinessRuleException("RETURN_HANDOVER_INVALID_STATE", "Drone can only be returned for completed, in-progress or cancelled missions.");

        var handover = mission.DroneHandovers.FirstOrDefault(x => x.DroneId == droneId && x.ReturnedAt == null && x.Status == DroneHandoverStatus.Accepted);
        if (handover == null)
            throw new NotFoundException("Active drone handover not found for this mission.");

        handover.ReturnedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(condition))
        {
            handover.Condition = string.IsNullOrWhiteSpace(handover.Condition)
                ? $"Return: {condition}"
                : $"{handover.Condition} | Return: {condition}";
        }

        var drone = await _db.Uavs.FirstOrDefaultAsync(d => d.Id == droneId, ct);
        if (drone != null && !await MissionResourceGuard.UavFlyingInOtherMissionAsync(_db, droneId, mission.Id, ct))
        {
            drone.FlightStatus = DroneStatus.Idle;
            drone.UpdatedAt = DateTime.UtcNow;
        }

        Audit(mission.Id, "DRONE_HANDOVER_RETURNED");
        await _db.SaveChangesAsync(ct);
        return handover;
    }

    public async Task<MissionFlightLog> UploadFlightLogAsync(Guid missionId, UploadFlightLogRequest request, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await AccessibleMission(missionId, ct, true);
        EnsureAssignedInspectorOrManager(mission);
        if (mission.Status is MissionStatus.Draft or MissionStatus.PendingAcceptance)
            throw new BusinessRuleException("FLIGHT_LOG_INVALID_STATE", "Cannot upload flight log before mission execution starts.");

        if (string.IsNullOrWhiteSpace(request.GpsTrack))
            throw new BusinessRuleException("GPS_TRACK_REQUIRED", "GPS track data is required.");

        try
        {
            using var doc = JsonDocument.Parse(request.GpsTrack);
        }
        catch (JsonException)
        {
            throw new BusinessRuleException("INVALID_GPS_TRACK_JSON", "GPS track must be a valid JSON array or object.");
        }

        if (request.MinBatteryRecorded < 0 || request.MinBatteryRecorded > 100)
            throw new BusinessRuleException("INVALID_BATTERY_LEVEL", "MinBatteryRecorded must be between 0 and 100.");

        if (request.MaxAltitudeM < 0)
            throw new BusinessRuleException("INVALID_ALTITUDE", "MaxAltitudeM cannot be negative.");

        if (request.FlightDurationSeconds < 0)
            throw new BusinessRuleException("INVALID_FLIGHT_DURATION", "FlightDurationSeconds cannot be negative.");

        var log = new MissionFlightLog
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            GpsTrack = request.GpsTrack,
            MinBatteryRecorded = request.MinBatteryRecorded,
            MaxAltitudeM = request.MaxAltitudeM,
            FlightDurationSeconds = request.FlightDurationSeconds,
            ConnectionStatus = !string.IsNullOrWhiteSpace(request.ConnectionStatus) ? request.ConnectionStatus : "Normal",
            RecordedAt = DateTime.UtcNow,
            CreatedBy = _current.UserId
        };

        _db.MissionFlightLogs.Add(log);
        Audit(mission.Id, "FLIGHT_LOG_UPLOADED");
        await _db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<IReadOnlyList<MissionFlightLog>> GetFlightLogsAsync(Guid missionId, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        await AccessibleMission(missionId, ct, false);
        return await _db.MissionFlightLogs
            .Where(x => x.MissionId == missionId)
            .OrderByDescending(x => x.RecordedAt)
            .ToListAsync(ct);
    }

    public async Task<IncidentReport> SubmitIncidentReportAsync(Guid missionId, SubmitIncidentReportRequest request, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await AccessibleMission(missionId, ct, true);
        EnsureAssignedInspectorOrManager(mission);

        if (string.IsNullOrWhiteSpace(request.IncidentType))
            throw new BusinessRuleException("INCIDENT_TYPE_REQUIRED", "IncidentType is required.");

        if (string.IsNullOrWhiteSpace(request.Severity))
            throw new BusinessRuleException("SEVERITY_REQUIRED", "Severity is required.");

        var validSeverities = new[] { "Low", "Medium", "High", "Critical" };
        if (!validSeverities.Any(s => s.Equals(request.Severity, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException("INVALID_SEVERITY", "Severity must be one of: Low, Medium, High, Critical.");

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length < 10)
            throw new BusinessRuleException("DESCRIPTION_TOO_SHORT", "Description must have at least 10 characters.");

        // Sự cố có thể không xảy ra tại cột nào (TowerId NULL).
        Guid? resolvedTowerId = null;
        if (request.TowerId.HasValue && request.TowerId.Value != Guid.Empty)
        {
            var towerExists = await _db.Towers.AnyAsync(a => a.Id == request.TowerId.Value, ct);
            if (!towerExists) throw new NotFoundException("Tower", request.TowerId.Value);
            resolvedTowerId = request.TowerId.Value;
        }

        var isCritical = request.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase);

        var incident = new IncidentReport
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            ReportedBy = _current.UserId,
            TowerId = resolvedTowerId,
            IncidentType = request.IncidentType,
            Severity = char.ToUpperInvariant(request.Severity[0]) + request.Severity[1..].ToLowerInvariant(),
            Description = request.Description.Trim(),
            Status = "Reported",
            ReportedAt = DateTime.UtcNow,
            CreatedBy = _current.UserId
        };

        _db.IncidentReports.Add(incident);
        Audit(mission.Id, isCritical ? "CRITICAL_INCIDENT_REPORTED" : "INCIDENT_REPORTED");

        if (isCritical)
        {
            Notify(mission.ManagerId, mission, "CRITICAL_INCIDENT", $"{request.IncidentType}: {request.Description}");
        }

        await _db.SaveChangesAsync(ct);
        return incident;
    }

    public async Task<IReadOnlyList<IncidentReport>> GetIncidentReportsAsync(Guid missionId, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        await AccessibleMission(missionId, ct, false);
        return await _db.IncidentReports
            .Include(x => x.Reporter)
            .Include(x => x.Tower)
            .Where(x => x.MissionId == missionId)
            .OrderByDescending(x => x.ReportedAt)
            .ToListAsync(ct);
    }

    private static MissionDetectionBoundingBoxDto? ParseBoundingBoxDto(string? rawBoundingBox)
    {
        if (string.IsNullOrWhiteSpace(rawBoundingBox)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawBoundingBox);
            var root = doc.RootElement;
            if (TryGetJsonDouble(root, "x", out var x) &&
                TryGetJsonDouble(root, "y", out var y) &&
                TryGetJsonDouble(root, "width", out var w) &&
                TryGetJsonDouble(root, "height", out var h))
            {
                return new MissionDetectionBoundingBoxDto { X = x, Y = y, Width = w, Height = h };
            }
            if (TryGetJsonDouble(root, "x1", out var x1) &&
                TryGetJsonDouble(root, "y1", out var y1) &&
                TryGetJsonDouble(root, "x2", out var x2) &&
                TryGetJsonDouble(root, "y2", out var y2))
            {
                return new MissionDetectionBoundingBoxDto { X = x1, Y = y1, Width = Math.Max(0, x2 - x1), Height = Math.Max(0, y2 - y1) };
            }
        }
        catch { }
        return null;
    }

    private static bool TryGetJsonDouble(System.Text.Json.JsonElement elem, string propName, out double val)
    {
        val = 0;
        foreach (var p in elem.EnumerateObject())
        {
            if (string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase))
            {
                if (p.Value.ValueKind == System.Text.Json.JsonValueKind.Number) return p.Value.TryGetDouble(out val);
                if (p.Value.ValueKind == System.Text.Json.JsonValueKind.String && double.TryParse(p.Value.GetString(), out val)) return true;
            }
        }
        return false;
    }

    private IQueryable<Tower> AssetsForRegion(Guid regionId) => _db.Towers.Where(x => x.RegionId == regionId || x.Region!.ParentId == regionId);
    private static Geometry? ParseBoundarySafe(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            var trimmed = text.Trim();
            Geometry? g = null;
            if (trimmed.StartsWith("{"))
            {
                var options = new System.Text.Json.JsonSerializerOptions();
                options.Converters.Add(new NetTopologySuite.IO.Converters.GeoJsonConverterFactory());
                g = System.Text.Json.JsonSerializer.Deserialize<Geometry>(trimmed, options);
            }
            else
            {
                g = new WKTReader().Read(trimmed);
            }

            if (g == null || !g.IsValid || g.IsEmpty) return null;
            g.SRID = 4326;
            return g;
        }
        catch
        {
            return null;
        }
    }

    private static Geometry ParseBoundary(string wkt)
    {
        var g = ParseBoundarySafe(wkt);
        if (g == null) throw new BusinessRuleException("INVALID_GEOMETRY");
        return g;
    }
    private async Task<Mission> ManagedMission(Guid id, CancellationToken ct, bool graph = false) { await RequireActiveCaller(ct); await RequireManageMission(id, ct); return await MissionQuery(graph).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Mission", id); }
    private async Task<Mission> AccessibleMission(Guid id, CancellationToken ct, bool graph = false)
    {
        await RequireActiveCaller(ct);
        var global = IsGlobal;
        var uid = _current.UserId;
        var userRegionScopes = await _db.UserGeographicScopes
            .Where(x => x.UserId == uid)
            .Select(x => (Guid?)x.RegionId)
            .ToListAsync(ct);

        var m = await MissionQuery(graph).SingleOrDefaultAsync(x => x.Id == id && (
            global ||
            x.ManagerId == uid ||
            x.Assignments.Any(a => a.UserId == uid && a.Status == MissionAssignmentStatus.Active) ||
            userRegionScopes.Contains(x.RegionId)
        ), ct);
        return m ?? throw new ForbiddenException("MISSION_ACCESS_DENIED");
    }
    private IQueryable<Mission> MissionQuery(bool graph)
    {
        var q = _db.Missions.Include(x => x.Assignments).ThenInclude(a => a.User).AsQueryable();
        return graph ? q.Include(x => x.CheckIns).Include(x => x.DroneHandovers).Include(x => x.MissionTargets) : q;
    }
    private async Task RequireManageMission(Guid id, CancellationToken ct) { var region = await _db.Missions.Where(x => x.Id == id).Select(x => x.RegionId).SingleOrDefaultAsync(ct) ?? throw new BusinessRuleException("MISSION_REGION_REQUIRED"); await RequireManageRegion(region, ct); }
    private async Task RequireManageRegion(Guid region, CancellationToken ct) { if (IsGlobal) return; if (!_current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase) || !await _db.UserGeographicScopes.AnyAsync(x => x.UserId == _current.UserId && x.RegionId == region, ct)) throw new ForbiddenException("REGION_MANAGEMENT_SCOPE_REQUIRED"); }
    private async Task RequireActiveCaller(CancellationToken ct) { if (!_current.IsAuthenticated || _current.UserId == Guid.Empty) throw new ForbiddenException("AUTHENTICATION_REQUIRED"); var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == _current.UserId, ct); if (user == null || !IsActive(user.Status)) throw new ForbiddenException("ACTIVE_USER_REQUIRED"); }
    private bool IsGlobal => _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase);
    private static bool IsActive(string status) => status.Equals("Active", StringComparison.OrdinalIgnoreCase) || status.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
    /// <summary>BR-32: personnel, UAV, targets and priority are frozen once the mission has started (also while suspended after the start).</summary>
    private static void RequirePreExecution(Mission m) { if (m.Status is MissionStatus.InProgress or MissionStatus.Completed or MissionStatus.Cancelled || m.StartedAt != null) throw new BusinessRuleException("MISSION_IMMUTABLE_AFTER_START"); }

    /// <summary>Table 59: a member answers (accept/postpone) only while the mission waits for or holds its team.</summary>
    private static void RequireRespondableState(Mission m)
    {
        if (m.Status is not (MissionStatus.PendingAcceptance or MissionStatus.Assigned or MissionStatus.Preparing or MissionStatus.Ready) || m.StartedAt != null)
            throw new BusinessRuleException("INVALID_MISSION_STATE", $"Cannot respond to an assignment while the mission is {m.Status}.");
    }

    private static (DateTime? Start, DateTime? End) PlannedWindow(Mission m)
    {
        var start = m.PlannedStart;
        var end = m.PlannedEnd ?? start?.AddHours(2);
        return (start, end);
    }

    private static Guid RequireRegion(Mission m) => m.RegionId ?? throw new BusinessRuleException("MISSION_REGION_REQUIRED");

    private static bool IsInspectorRole(string? role) =>
        string.Equals(role, "INSPECTOR", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "PILOT", StringComparison.OrdinalIgnoreCase);

    /// <summary>Bumps the mission version so concurrent changes to the same mission conflict (BR-07).</summary>
    private static void TouchMission(Mission m) { m.Version++; m.UpdatedAt = DateTime.UtcNow; }

    /// <summary>A member leaving the mission loses their check-in and hands back the UAV they hold.</summary>
    private static void CloseMemberFieldRecords(Mission m, Guid userId)
    {
        var now = DateTime.UtcNow;
        foreach (var c in m.CheckIns.Where(c => !c.IsDeleted && c.UserId == userId && c.Status == MissionCheckInStatus.CheckedIn))
            c.Status = MissionCheckInStatus.Revoked;
        foreach (var h in m.DroneHandovers.Where(h => !h.IsDeleted && h.ReceivedBy == userId && h.ReturnedAt == null))
        {
            h.ReturnedAt = h.Status == DroneHandoverStatus.Rejected ? h.ReceivedAt : now;
            if (h.Status == DroneHandoverStatus.Accepted) h.Status = DroneHandoverStatus.Returned;
        }
    }

    /// <summary>
    /// BR-30 acceptance by the caller. A repeated accept returns the assignment unchanged (UC-MF02-07 A2); otherwise
    /// the mission must still be waiting for or holding its team (Table 59).
    /// </summary>
    private (MissionAssignment Assignment, bool Changed, bool AllConfirmed) AcceptCurrentUser(Mission mission)
    {
        var assignment = mission.ActiveAssignments().FirstOrDefault(x => x.UserId == _current.UserId)
            ?? throw new NotFoundException("MissionAssignment", _current.UserId);
        if (assignment.ResponseStatus == MissionAssignmentResponse.Accepted)
            return (assignment, false, false);
        RequireRespondableState(mission);

        assignment.ResponseStatus = MissionAssignmentResponse.Accepted;
        assignment.RespondedAt = DateTime.UtcNow;
        assignment.Version++;
        TouchMission(mission);

        var allConfirmed = false;
        if (mission.Status == MissionStatus.PendingAcceptance)
        {
            allConfirmed = mission.CheckAcceptance();
            if (allConfirmed)
            {
                Audit(mission.Id, "MISSION_CONFIRMED");
                NotifyOnce(new[] { mission.ManagerId }, mission, "MISSION_CONFIRMED");
            }
        }
        else
        {
            mission.RecalculateReadiness();
        }

        Audit(mission.Id, "ASSIGNMENT_ACCEPTED");
        return (assignment, true, allConfirmed);
    }

    private static IEnumerable<Guid> MemberIds(Mission m) => m.ActiveAssignments().Select(a => a.UserId);

    /// <summary>One stored notification per user, never to an empty id (no FK row for Guid.Empty).</summary>
    private void NotifyOnce(IEnumerable<Guid> userIds, Mission m, string type, string? role = null)
    {
        foreach (var userId in userIds.Where(id => id != Guid.Empty).Distinct())
            Notify(userId, m, type, role);
    }

    /// <summary>BR-36: an accepted, unreturned handover of the UAV in another mission that is still running.</summary>
    private Task<bool> OpenHandoverInOtherActiveMissionAsync(Guid droneId, Guid missionId, CancellationToken ct) =>
        _db.DroneHandovers.AnyAsync(h => h.DroneId == droneId && h.MissionId != missionId
            && h.Status == DroneHandoverStatus.Accepted && h.ReturnedAt == null
            && _db.Missions.Any(m => m.Id == h.MissionId && m.Status != MissionStatus.Completed && m.Status != MissionStatus.Cancelled), ct);
    private void EnsureAssignedInspectorOrManager(Mission m)
    {
        if (IsGlobal || _current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase))
            return;

        var uid = _current.UserId;
        var isAssigned = (m.PrimaryInspectorId().HasValue && m.PrimaryInspectorId().Value == uid) ||
                         m.Assignments.Any(a => a.UserId == uid && a.Status == MissionAssignmentStatus.Active);

        if (!isAssigned)
            throw new ForbiddenException("USER_NOT_ASSIGNED_TO_MISSION");
    }
    private void Audit(Guid id, string action, string oldValues = "{}", string newValues = "{}") => _db.AuditLogs.Add(new AuditLog { UserId = _current.UserId, TableName = "Missions", RecordId = id, ActionType = action.Length > 100 ? action[..100] : action, OldValues = oldValues, NewValues = newValues, IpAddress = _current.IpAddress ?? "", UserAgent = _current.UserAgent ?? "" });
    private static (string Title, string Body) GetNotificationContent(string type, Mission m, string? role)
    {
        var roleText = !string.IsNullOrWhiteSpace(role) ? $"vai trò {role}" : "nhiệm vụ";
        var deadlineText = m.ConfirmationDeadline.HasValue 
            ? $" Hạn chót xác nhận: {m.ConfirmationDeadline.Value:dd/MM/yyyy HH:mm} (UTC)." 
            : string.Empty;
        var instructionsText = !string.IsNullOrWhiteSpace(m.ManagerInstructions)
            ? $" Lời dặn: \"{m.ManagerInstructions}\""
            : string.Empty;

        return type switch
        {
            "MISSION_CANCELLED" => (
                $"[HỦY NHIỆM VỤ] {m.MissionCode} đã bị hủy",
                $"Nhiệm vụ \"{m.Title}\" ({m.MissionCode}) đã bị hủy bỏ bởi điều phối viên."
            ),
            "MISSION_SUSPENDED" => (
                $"[ĐÌNH CHỈ BAY] Lệnh tạm đình chỉ bay khẩn cấp: {m.MissionCode}",
                $"Nhiệm vụ \"{m.Title}\" ({m.MissionCode}) đã bị tạm đình chỉ. Tất cả hoạt động bay phải dừng ngay lập tức."
            ),
            "MISSION_RESUMED" => (
                $"[TIẾP TỤC BAY] Nhiệm vụ {m.MissionCode} đã được kích hoạt lại",
                $"Nhiệm vụ \"{m.Title}\" ({m.MissionCode}) đã được quản lý cho phép tiếp tục hoạt động."
            ),
            "MISSION_POSTPONED" => (
                $"[HOÃN NHIỆM VỤ] Toàn bộ nhiệm vụ {m.MissionCode} đã hoãn lịch",
                $"Nhiệm vụ \"{m.Title}\" ({m.MissionCode}) đã bị hoãn bởi quản lý.{(!string.IsNullOrWhiteSpace(m.PostponeReason) ? $" Lý do: {m.PostponeReason}." : "")}"
            ),
            "ASSIGNMENT_POSTPONED" => (
                $"[XIN HOÃN PHÂN CÔNG] Nhân sự xin hoãn nhận nhiệm vụ: {m.MissionCode}",
                $"Có nhân sự xin hoãn phân công trong nhiệm vụ \"{m.Title}\" ({m.MissionCode}). Vui lòng xem xét điều phối lại."
            ),
            "MISSION_CONFIRMED" => (
                $"[ĐÃ XÁC NHẬN] Nhiệm vụ {m.MissionCode} đã sẵn sàng",
                $"Tất cả nhân sự bắt buộc đã xác nhận tiếp nhận nhiệm vụ \"{m.Title}\" ({m.MissionCode})."
            ),
            "MISSION_ASSIGNMENT_REMOVED" => (
                $"[THU HỒI PHÂN CÔNG] Thu hồi phân công nhiệm vụ {m.MissionCode}",
                $"Phân công {roleText} của bạn cho nhiệm vụ \"{m.Title}\" ({m.MissionCode}) đã bị thu hồi."
            ),
            "MISSION_REMINDER" => (
                $"[NHẮC NHỞ] Hạn chót xác nhận nhiệm vụ: {m.MissionCode}",
                $"Nhắc nhở: Vui lòng xác nhận tiếp nhận {roleText} cho nhiệm vụ \"{m.Title}\".{deadlineText}"
            ),
            _ => (
                $"[MF02 ĐIỀU PHỐI] Yêu cầu xác nhận nhiệm vụ: {m.MissionCode}",
                $"Bạn được phân công tham gia {roleText} cho nhiệm vụ \"{m.Title}\".{deadlineText}{instructionsText}"
            )
        };
    }

    private void Notify(Guid userId, Mission m, string type, string? role = null)
    {
        var (title, body) = GetNotificationContent(type, m, role);

        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type == "MISSION_DISPATCHED" ? "MISSION_DISPATCH" : type,
            ReferenceType = "MISSION",
            ReferenceId = m.Id,
            Title = title,
            Body = body,
            SentAt = DateTime.UtcNow
        });
    }
    public async Task<Mission> UpdatePriorityAsync(Guid missionId, UpdateMissionPriorityRequest request, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct);
        RequirePreExecution(mission);

        // BR-06: EmergencyReason is required when Priority is EMERGENCY
        if (request.Priority == MissionPriority.Emergency)
        {
            if (string.IsNullOrWhiteSpace(request.EmergencyReason) || request.EmergencyReason.Trim().Length < 10)
            {
                throw new BusinessRuleException("EMERGENCY_REASON_REQUIRED", "EmergencyReason is required (min 10 characters) when Priority is EMERGENCY.");
            }
        }

        await _db.Entry(mission).Collection(m => m.PriorityDefects).Query().Include(p => p.Category).LoadAsync(ct);
        var oldValues = JsonSerializer.Serialize(new
        {
            Priority = mission.Priority.ToString(),
            Objective = mission.Objective.ToString(),
            PriorityDefects = mission.PriorityDefectCodes(),
            mission.TriggerReason
        });

        mission.Priority = request.Priority;
        if (request.Objective.HasValue)
        {
            mission.Objective = request.Objective.Value;
        }
        if (request.PriorityDefects != null)
        {
            await MissionRepository.ApplyPriorityDefectsAsync(_db, mission, request.PriorityDefects, ct);
        }
        if (request.Priority == MissionPriority.Emergency)
        {
            mission.TriggerReason = request.EmergencyReason?.Trim();
        }

        var newValues = JsonSerializer.Serialize(new
        {
            Priority = mission.Priority.ToString(),
            Objective = mission.Objective.ToString(),
            PriorityDefects = mission.PriorityDefectCodes(),
            mission.TriggerReason
        });

        // BR-09: Mission priority creation and changes must be recorded in Audit Log
        Audit(mission.Id, "MISSION_PRIORITY_UPDATED", oldValues, newValues);
        await _db.SaveChangesAsync(ct);
        return mission;
    }

    public async Task<CheckResourceConflictsResponse> CheckResourceConflictsAsync(CheckResourceConflictsRequest request, CancellationToken ct)
    {
        if (request.StartAt >= request.EndAt)
        {
            throw new BusinessRuleException("INVALID_TIME_RANGE", "StartAt must be earlier than EndAt.");
        }

        var userList = (request.UserIds ?? Array.Empty<Guid>()).Distinct().ToList();
        var droneList = (request.DroneIds ?? Array.Empty<Guid>()).Distinct().ToList();

        // BR-19: read the bookings themselves. Including the (soft-deletable) mission would inner-join away
        // bookings that the database exclusion constraint still enforces.
        var query = _db.ResourceBookings
            .AsNoTracking()
            .Where(b => b.Status == ResourceBookingStatus.Active &&
                        b.StartAt < request.EndAt &&
                        b.EndAt > request.StartAt &&
                        ((b.UserId.HasValue && userList.Contains(b.UserId.Value)) || (b.DroneId.HasValue && droneList.Contains(b.DroneId.Value))));

        if (request.ExcludeMissionId.HasValue)
        {
            query = query.Where(b => b.MissionId != request.ExcludeMissionId.Value);
        }

        var overlappingBookings = await query.ToListAsync(ct);
        var missionIds = overlappingBookings.Select(b => b.MissionId).Distinct().ToList();
        var missions = await _db.Missions.IgnoreQueryFilters().AsNoTracking()
            .Where(m => missionIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);
        var bookedUserIds = overlappingBookings.Where(b => b.UserId.HasValue).Select(b => b.UserId!.Value).Distinct().ToList();
        var users = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => bookedUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);
        var bookedDroneIds = overlappingBookings.Where(b => b.DroneId.HasValue).Select(b => b.DroneId!.Value).Distinct().ToList();
        var bookedDrones = await _db.Uavs.IgnoreQueryFilters().AsNoTracking()
            .Where(d => bookedDroneIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);
        foreach (var b in overlappingBookings)
        {
            b.Mission = missions.GetValueOrDefault(b.MissionId);
            b.User = b.UserId.HasValue ? users.GetValueOrDefault(b.UserId.Value) : null;
            b.Drone = b.DroneId.HasValue ? bookedDrones.GetValueOrDefault(b.DroneId.Value) : null;
        }
        var conflicts = new List<ResourceConflictItemDto>();

        foreach (var b in overlappingBookings)
        {
            if (b.UserId.HasValue && userList.Contains(b.UserId.Value))
            {
                conflicts.Add(new ResourceConflictItemDto(
                    ResourceType: "Personnel",
                    ResourceId: b.UserId.Value,
                    ResourceName: b.User?.FullName ?? b.UserId.ToString(),
                    ConflictingMissionId: b.MissionId,
                    ConflictingMissionCode: b.Mission?.MissionCode ?? b.MissionId.ToString(),
                    ConflictingMissionTitle: b.Mission?.Title ?? string.Empty,
                    ConflictStart: b.StartAt,
                    ConflictEnd: b.EndAt,
                    ConflictingMissionPriority: b.Mission?.Priority ?? MissionPriority.Normal
                ));
            }

            if (b.DroneId.HasValue && droneList.Contains(b.DroneId.Value))
            {
                conflicts.Add(new ResourceConflictItemDto(
                    ResourceType: "Drone",
                    ResourceId: b.DroneId.Value,
                    ResourceName: b.Drone?.UavCode ?? b.DroneId.ToString(),
                    ConflictingMissionId: b.MissionId,
                    ConflictingMissionCode: b.Mission?.MissionCode ?? b.MissionId.ToString(),
                    ConflictingMissionTitle: b.Mission?.Title ?? string.Empty,
                    ConflictStart: b.StartAt,
                    ConflictEnd: b.EndAt,
                    ConflictingMissionPriority: b.Mission?.Priority ?? MissionPriority.Normal
                ));
            }
        }

        // BR-07: Resource Revalidation - UAV availability & health warnings
        var warnings = new List<ResourceAvailabilityWarningDto>();
        if (droneList.Count > 0)
        {
            var drones = await _db.Uavs.Where(u => droneList.Contains(u.Id)).ToListAsync(ct);
            foreach (var d in drones)
            {
                if (d.OperationalStatus != DroneOperationalStatus.Available)
                {
                    warnings.Add(new ResourceAvailabilityWarningDto(
                        ResourceId: d.Id,
                        ResourceType: "Drone",
                        ResourceName: d.UavCode,
                        WarningType: "OPERATIONAL_STATUS",
                        Message: $"Drone {d.UavCode} hiện ở trạng thái '{d.OperationalStatus}', không sẵn sàng."
                    ));
                }
                if (d.TechnicalHealth == TechnicalHealth.Critical)
                {
                    warnings.Add(new ResourceAvailabilityWarningDto(
                        ResourceId: d.Id,
                        ResourceType: "Drone",
                        ResourceName: d.UavCode,
                        WarningType: "TECHNICAL_HEALTH_CRITICAL",
                        Message: $"Drone {d.UavCode} có sức khỏe kỹ thuật CRITICAL, không đủ điều kiện bay."
                    ));
                }
            }
        }

        return new CheckResourceConflictsResponse(
            HasConflict: conflicts.Count > 0,
            Conflicts: conflicts,
            Warnings: warnings
        );
    }

    private async Task SaveConcurrency(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityNames = string.Join(", ", ex.Entries.Select(e => $"{e.Metadata.ClrType.Name} ({e.State})"));
            throw new BusinessRuleException("MISSION_CONCURRENCY_CONFLICT", $"{entityNames}: {ex.Message}");
        }
        catch (DbUpdateException ex) when (DbConstraintErrors.Translate(ex) is { } translated)
        {
            // e.g. the booking exclusion constraint lost a race against another request (BR-19).
            throw translated;
        }
    }
}

