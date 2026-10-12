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

    [Obsolete("Use PreMissionAssessmentService.CreateMissionFromAssessmentAsync instead.")]
    public async Task<Mission> CreateAsync(Mf01CreateMission request, CancellationToken ct)
    {
        await RequireActiveCaller(ct); await RequireManageRegion(request.RegionId, ct);

        var resolvedAssignedUserId = await ResolveUserIdAsync(request.AssignedToUserId, ct);
        var resolvedDroneId = await ResolveDroneIdAsync(request.DroneId, ct);
        var resolvedAssignments = new List<MissionAssignmentItemRequest>();
        if (request.Assignments != null && request.Assignments.Count > 0)
        {
            foreach (var a in request.Assignments)
            {
                var resolvedUid = await ResolveUserIdAsync(a.UserId, ct);
                if (resolvedUid.HasValue)
                {
                    resolvedAssignments.Add(a with { UserId = resolvedUid.Value });
                }
            }
        }

        if (!resolvedAssignedUserId.HasValue)
        {
            var inspItem = resolvedAssignments.FirstOrDefault(x =>
                string.Equals(x.Role, "INSPECTOR", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Role, "PILOT", StringComparison.OrdinalIgnoreCase));
            if (inspItem != null)
            {
                resolvedAssignedUserId = inspItem.UserId;
            }
            else if (resolvedAssignments.Count > 0)
            {
                resolvedAssignedUserId = resolvedAssignments[0].UserId;
            }
        }

        if (!resolvedAssignedUserId.HasValue || resolvedAssignedUserId.Value == Guid.Empty)
        {
            throw new BusinessRuleException("INSPECTOR_REQUIRED", "Assigned inspector or pilot is required for mission creation.");
        }

        if (request.PreMissionAssessmentId.HasValue)
        {
            var existingMission = await _db.Missions
                .Include(x => x.Assignments)
                .FirstOrDefaultAsync(x => x.PreMissionAssessmentId == request.PreMissionAssessmentId.Value, ct);
            if (existingMission != null)
            {
                foreach (var a in resolvedAssignments)
                {
                    var existing = existingMission.Assignments.FirstOrDefault(x => x.UserId == a.UserId);
                    if (existing != null)
                    {
                        if (!string.IsNullOrWhiteSpace(a.Role)) existing.AssignmentRole = a.Role;
                        if (a.IsRequired.HasValue) existing.IsRequired = a.IsRequired.Value;
                    }
                    else
                    {
                        existingMission.Assignments.Add(new MissionAssignment
                        {
                            MissionId = existingMission.Id,
                            UserId = a.UserId,
                            AssignmentRole = !string.IsNullOrWhiteSpace(a.Role) ? a.Role : "PILOT",
                            AssignedByUserId = _current.UserId,
                            IsRequired = a.IsRequired ?? true,
                            ResponseStatus = MissionAssignmentResponse.Pending
                        });
                    }
                }
                if (resolvedDroneId.HasValue && (!existingMission.UavId.HasValue || existingMission.UavId.Value == Guid.Empty))
                {
                    existingMission.UavId = resolvedDroneId.Value;
                }
                if (resolvedAssignedUserId.HasValue && (!existingMission.InspectorId.HasValue || existingMission.InspectorId.Value == Guid.Empty))
                {
                    existingMission.InspectorId = resolvedAssignedUserId.Value;
                    existingMission.AssignedToUserId = resolvedAssignedUserId.Value;
                }
                if (!existingMission.InspectorId.HasValue || existingMission.InspectorId.Value == Guid.Empty)
                {
                    var ins = existingMission.Assignments.FirstOrDefault(x => x.AssignmentRole.Equals("Inspector", StringComparison.OrdinalIgnoreCase) || x.AssignmentRole.Equals("Pilot", StringComparison.OrdinalIgnoreCase));
                    if (ins != null) existingMission.InspectorId = ins.UserId;
                }
                await _db.SaveChangesAsync(ct);
                return existingMission;
            }
        }
        var region = await _db.Regions.SingleOrDefaultAsync(x => x.Id == request.RegionId, ct)
            ?? throw new NotFoundException("Region", request.RegionId);
        if (region.IsDeleted) throw new BusinessRuleException("REGION_INACTIVE");
        if (request.PlannedStart >= request.PlannedEnd) throw new BusinessRuleException("INVALID_PLANNED_TIME");
        var effectiveMissionType = request.MissionType;
        InspectionSchedule? schedule = null;
        if (effectiveMissionType == MissionType.Scheduled)
        {
            if (request.ScheduleId is not null)
            {
                schedule = await _db.InspectionSchedules.SingleOrDefaultAsync(x => x.Id == request.ScheduleId, ct)
                    ?? throw new NotFoundException("InspectionSchedule", request.ScheduleId.Value);
                if (!schedule.IsActive || schedule.RegionId != request.RegionId) throw new BusinessRuleException("SCHEDULE_REGION_MISMATCH");
            }
            else
            {
                effectiveMissionType = MissionType.AdHoc;
            }
        }
        else if (request.ScheduleId is not null) throw new BusinessRuleException("AD_HOC_SCHEDULE_NOT_ALLOWED");

        if (request.Priority == MissionPriority.Emergency)
        {
            if (string.IsNullOrWhiteSpace(request.EmergencyReason) || request.EmergencyReason.Trim().Length < 10)
            {
                throw new BusinessRuleException("EMERGENCY_REASON_REQUIRED", "EmergencyReason is required (min 10 characters) when Priority is EMERGENCY.");
            }
        }

        // Revalidate resource schedule conflicts (BR-04, BR-07)
        if (resolvedDroneId.HasValue)
        {
            var droneConflict = await _db.ResourceBookings
                .Include(b => b.Mission)
                .Where(b => b.DroneId == resolvedDroneId.Value &&
                            b.Status == ResourceBookingStatus.Active &&
                            b.StartAt < request.PlannedEnd &&
                            b.EndAt > request.PlannedStart)
                .FirstOrDefaultAsync(ct);
            if (droneConflict != null)
            {
                throw new BusinessRuleException("RESOURCE_BOOKING_CONFLICT",
                    $"Drone {resolvedDroneId.Value} is already booked in mission '{droneConflict.Mission?.MissionCode ?? droneConflict.MissionId.ToString()}' ({droneConflict.StartAt:yyyy-MM-dd HH:mm} - {droneConflict.EndAt:yyyy-MM-dd HH:mm}).");
            }
        }

        var allAssignedUserIds = new HashSet<Guid>();
        if (resolvedAssignedUserId.HasValue) allAssignedUserIds.Add(resolvedAssignedUserId.Value);
        foreach (var a in resolvedAssignments) allAssignedUserIds.Add(a.UserId);

        if (allAssignedUserIds.Count > 0)
        {
            var userConflict = await _db.ResourceBookings
                .Include(b => b.Mission)
                .Where(b => b.UserId.HasValue && allAssignedUserIds.Contains(b.UserId.Value) &&
                            b.Status == ResourceBookingStatus.Active &&
                            b.StartAt < request.PlannedEnd &&
                            b.EndAt > request.PlannedStart)
                .FirstOrDefaultAsync(ct);
            if (userConflict != null)
            {
                throw new BusinessRuleException("RESOURCE_BOOKING_CONFLICT",
                    $"Personnel {userConflict.UserId} is already booked in mission '{userConflict.Mission?.MissionCode ?? userConflict.MissionId.ToString()}' ({userConflict.StartAt:yyyy-MM-dd HH:mm} - {userConflict.EndAt:yyyy-MM-dd HH:mm}).");
            }
        }

        var status = request.ConfirmationDeadline.HasValue ? MissionStatus.PendingAcceptance : MissionStatus.Draft;
        var mission = new Mission
        {
            MissionCode = $"MS-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{RandomNumberGenerator.GetInt32(100, 1000)}",
            Title = request.Title,
            RegionId = request.RegionId,
            ScheduleId = request.ScheduleId,
            MissionType = effectiveMissionType,
            TriggerReason = request.TriggerReason,
            PlannedStart = request.PlannedStart,
            PlannedEnd = request.PlannedEnd,
            ScheduledStartAt = request.PlannedStart,
            Description = request.Description ?? string.Empty,
            ManagerId = _current.UserId,
            Priority = request.Priority,
            Objective = request.Objective,
            PriorityDefectsJson = JsonSerializer.Serialize(request.PriorityDefects ?? (IEnumerable<string>)Array.Empty<string>()),
            EmergencyReason = request.EmergencyReason,
            IsImmediate = request.IsImmediate,
            Status = status,
            ConfirmationDeadline = request.ConfirmationDeadline,
            ManagerInstructions = request.ManagerInstructions,
            AssignedToUserId = resolvedAssignedUserId ?? Guid.Empty,
            InspectorId = resolvedAssignedUserId,
            UavId = resolvedDroneId,
            PreMissionAssessmentId = request.PreMissionAssessmentId
        };

        if (resolvedAssignedUserId.HasValue && resolvedAssignedUserId.Value != Guid.Empty)
        {
            mission.Assignments.Add(new MissionAssignment
            {
                MissionId = mission.Id,
                UserId = resolvedAssignedUserId.Value,
                AssignmentRole = "INSPECTOR",
                AssignedByUserId = _current.UserId,
                IsRequired = true,
                ResponseStatus = MissionAssignmentResponse.Pending
            });
        }

        foreach (var a in resolvedAssignments)
        {
            var existing = mission.Assignments.FirstOrDefault(x => x.UserId == a.UserId);
            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(a.Role)) existing.AssignmentRole = a.Role;
                if (a.IsRequired.HasValue) existing.IsRequired = a.IsRequired.Value;
            }
            else
            {
                mission.Assignments.Add(new MissionAssignment
                {
                    MissionId = mission.Id,
                    UserId = a.UserId,
                    AssignmentRole = !string.IsNullOrWhiteSpace(a.Role) ? a.Role : "INSPECTOR",
                    AssignedByUserId = _current.UserId,
                    IsRequired = a.IsRequired ?? true,
                    ResponseStatus = MissionAssignmentResponse.Pending
                });
            }
        }

        if (request.PreMissionAssessmentId.HasValue)
        {
            var assessment = await _db.PreMissionAssessments
                .Include(x => x.Assets)
                .SingleOrDefaultAsync(x => x.Id == request.PreMissionAssessmentId.Value, ct);
            if (assessment != null)
            {
                assessment.ConsumedByMissionId = mission.Id;
                assessment.Status = PreMissionAssessmentStatus.Completed;
                if (mission.Boundary == null && assessment.ProposedBoundary != null)
                {
                    mission.Boundary = assessment.ProposedBoundary;
                }
                if (assessment.Assets != null && assessment.Assets.Count > 0 && !mission.MissionTargets.Any())
                {
                    mission.MissionTargets = assessment.Assets
                        .OrderBy(x => x.Sequence)
                        .Select(x => new MissionTarget
                        {
                            MissionId = mission.Id,
                            AssetId = x.AssetId,
                            Sequence = x.Sequence
                        })
                        .ToList();
                }
            }
        }

        _db.Missions.Add(mission);
        Audit(mission.Id, "MISSION_CREATED", "{}", JsonSerializer.Serialize(new
        {
            Priority = mission.Priority.ToString(),
            Objective = mission.Objective.ToString(),
            PriorityDefects = request.PriorityDefects,
            EmergencyReason = mission.EmergencyReason,
            IsImmediate = mission.IsImmediate
        }));

        var assignedUserIds = mission.Assignments.Select(x => x.UserId).Distinct().ToList();
        if (mission.InspectorId.HasValue && mission.InspectorId.Value != Guid.Empty && !assignedUserIds.Contains(mission.InspectorId.Value))
        {
            assignedUserIds.Add(mission.InspectorId!.Value);
        }

        foreach (var userId in assignedUserIds)
        {
            var role = mission.Assignments.FirstOrDefault(x => x.UserId == userId)?.AssignmentRole ?? "INSPECTOR";
            Notify(userId, mission, "MISSION_DISPATCH", role);
        }

        var dispatchLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            SenderName = _current.Username ?? "Quản lý",
            SenderRole = "MANAGER",
            Type = "DISPATCH",
            Content = string.IsNullOrWhiteSpace(request.ManagerInstructions)
                ? $"Nhiệm vụ {mission.MissionCode} đã được ban hành và giao cho phi công."
                : $"Nhiệm vụ {mission.MissionCode} đã được ban hành. Chỉ dẫn: {request.ManagerInstructions}",
            CreatedAt = DateTime.UtcNow
        };
        _db.MissionCommunicationLogs.Add(dispatchLog);

        await _db.SaveChangesAsync(ct);

        if (_notifier != null)
        {
            var eventDto = new UavPms.Shared.Contracts.Events.MissionLifecycleEventDto
            {
                MissionId = mission.Id.ToString(),
                MissionCode = mission.MissionCode,
                MissionTitle = mission.Title,
                Type = "DISPATCHED",
                Status = "PENDING_CONFIRMATION",
                ActorRole = "MANAGER",
                ActorId = _current.UserId.ToString(),
                ActorName = _current.Username ?? "Quản lý",
                ConfirmationDeadline = mission.ConfirmationDeadline?.ToString("o"),
                ManagerInstructions = mission.ManagerInstructions,
                InspectorId = mission.InspectorId != Guid.Empty ? mission.InspectorId?.ToString() ?? string.Empty : null,
                AssignedUserIds = assignedUserIds.Select(u => u.ToString()).ToList(),
                ManagerId = mission.ManagerId.ToString(),
                Timestamp = DateTime.UtcNow,
                Log = new UavPms.Shared.Contracts.Events.MissionCommunicationLogDto
                {
                    Id = dispatchLog.Id.ToString(),
                    SenderId = _current.UserId.ToString(),
                    SenderName = dispatchLog.SenderName,
                    SenderRole = dispatchLog.SenderRole,
                    Type = dispatchLog.Type,
                    Content = dispatchLog.Content,
                    Timestamp = dispatchLog.CreatedAt
                }
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }

        return mission;
    }

    public async Task<IReadOnlyList<Asset>> ResolveScopeAsync(Guid missionId, string boundaryWkt, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct);
        var boundary = ParseBoundarySafe(boundaryWkt) ?? mission.Boundary;
        var regionId = mission.RegionId ?? throw new BusinessRuleException("MISSION_REGION_REQUIRED");
        if (boundary == null)
        {
            return await AssetsForRegion(regionId).ToListAsync(ct);
        }
        return await AssetsForRegion(regionId).Where(x => x.Location != null && boundary.Covers(x.Location)).ToListAsync(ct);
    }

    public async Task ConfirmAssetsAsync(Guid missionId, string boundaryWkt, IReadOnlyCollection<Guid> assetIds, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct); RequirePreExecution(mission);
        if (assetIds.Count == 0) throw new BusinessRuleException("MISSION_TARGET_REQUIRED");
        var distinct = assetIds.Distinct().ToArray(); if (distinct.Length != assetIds.Count) throw new BusinessRuleException("DUPLICATE_ASSET");
        var regionId = mission.RegionId ?? throw new BusinessRuleException("MISSION_REGION_REQUIRED");
        var assets = await AssetsForRegion(regionId).Where(x => distinct.Contains(x.Id)).ToListAsync(ct);
        if (assets.Count != distinct.Length) throw new ForbiddenException("ASSET_OUTSIDE_REGION_OR_SCOPE");

        var boundary = ParseBoundarySafe(boundaryWkt) ?? mission.Boundary;
        if (boundary == null && assets.Any(x => x.Location != null))
        {
            var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(4326);
            var validPoints = assets.Where(x => x.Location != null).Select(x => x.Location is Point pt ? pt : factory.CreatePoint(x.Location!.Coordinate)).ToArray();
            var mp = factory.CreateMultiPoint(validPoints);
            var hull = mp.ConvexHull();
            boundary = (hull is Polygon poly) ? poly.Buffer(0.002) : hull.Buffer(0.002);
            boundary.SRID = 4326;
        }

        _db.MissionTargets.RemoveRange(await _db.MissionTargets.Where(x => x.MissionId == mission.Id).ToListAsync(ct));
        _db.MissionTargets.AddRange(distinct.Select((id, i) => new MissionTarget { MissionId = mission.Id, AssetId = id, Sequence = i + 1 }));
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
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == request.UserId, ct) ?? throw new NotFoundException("User", request.UserId);
        if (!IsActive(user.Status)) throw new BusinessRuleException("ASSIGNEE_INACTIVE");
        var existing = await _db.MissionAssignments.FirstOrDefaultAsync(x => x.MissionId == missionId && x.UserId == request.UserId && x.Status == MissionAssignmentStatus.Active, ct);
        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(request.AssignmentRole))
            {
                existing.AssignmentRole = request.AssignmentRole;
            }
            if (request.AssignmentRole.Equals("Inspector", StringComparison.OrdinalIgnoreCase) || request.AssignmentRole.Equals("Pilot", StringComparison.OrdinalIgnoreCase))
            {
                if (!mission.InspectorId.HasValue || mission.InspectorId.Value == Guid.Empty)
                {
                    mission.InspectorId = request.UserId;
                    mission.AssignedToUserId = request.UserId;
                }
            }
            await _db.SaveChangesAsync(ct);
            return existing;
        }
        var assignment = new MissionAssignment { MissionId = missionId, UserId = request.UserId, AssignmentRole = request.AssignmentRole, AssignedByUserId = _current.UserId };
        _db.MissionAssignments.Add(assignment); mission.Assignments.Add(assignment);
        if (request.AssignmentRole.Equals("Inspector", StringComparison.OrdinalIgnoreCase) || request.AssignmentRole.Equals("Pilot", StringComparison.OrdinalIgnoreCase))
        {
            if (!mission.InspectorId.HasValue || mission.InspectorId.Value == Guid.Empty)
            {
                mission.InspectorId = request.UserId;
                mission.AssignedToUserId = request.UserId;
            }
        }
        mission.RecalculateReadiness();
        Notify(request.UserId, mission, "MISSION_ASSIGNED", request.AssignmentRole); Audit(mission.Id, "MISSION_ASSIGNMENT_ADDED");
        await _db.SaveChangesAsync(ct); return assignment;
    }

    public async Task RemoveAssignmentAsync(Guid missionId, Guid assignmentId, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct, true); RequirePreExecution(mission);
        var assignment = mission.Assignments.SingleOrDefault(x => x.Id == assignmentId && x.Status == MissionAssignmentStatus.Active)
            ?? throw new NotFoundException("MissionAssignment", assignmentId);
        assignment.Status = MissionAssignmentStatus.Revoked; assignment.EndedAt = DateTime.UtcNow; mission.RecalculateReadiness();
        Notify(assignment.UserId, mission, "MISSION_ASSIGNMENT_REMOVED"); Audit(mission.Id, "MISSION_ASSIGNMENT_REMOVED");
        await _db.SaveChangesAsync(ct);
    }

    public async Task AssignDroneAsync(Guid missionId, Guid droneId, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct, true); RequirePreExecution(mission);
        var drone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == droneId, ct) ?? throw new NotFoundException("Drone", droneId);
        if (drone.Status != DroneStatus.Idle && mission.UavId != droneId) throw new BusinessRuleException("DRONE_UNAVAILABLE");

        var plannedStart = mission.PlannedStart ?? mission.ScheduledStartAt;
        var plannedEnd = mission.PlannedEnd ?? (plannedStart.HasValue ? plannedStart.Value.AddHours(2) : (DateTime?)null);

        if (plannedStart.HasValue && plannedEnd.HasValue)
        {
            var conflictingDroneBooking = await _db.ResourceBookings
                .Include(b => b.Mission)
                .Where(b => b.DroneId == droneId &&
                            b.MissionId != missionId &&
                            b.Status == ResourceBookingStatus.Active &&
                            b.StartAt < plannedEnd.Value &&
                            b.EndAt > plannedStart.Value)
                .FirstOrDefaultAsync(ct);
            if (conflictingDroneBooking != null)
            {
                throw new BusinessRuleException("RESOURCE_BOOKING_CONFLICT",
                    $"Drone {droneId} is already booked in mission '{conflictingDroneBooking.Mission?.MissionCode ?? conflictingDroneBooking.MissionId.ToString()}' ({conflictingDroneBooking.StartAt:yyyy-MM-dd HH:mm} - {conflictingDroneBooking.EndAt:yyyy-MM-dd HH:mm}).");
            }

            var existingDroneBooking = await _db.ResourceBookings
                .FirstOrDefaultAsync(b => b.MissionId == missionId && b.DroneId.HasValue && b.Status == ResourceBookingStatus.Active, ct);
            if (existingDroneBooking != null)
            {
                if (existingDroneBooking.DroneId != droneId)
                {
                    existingDroneBooking.Status = ResourceBookingStatus.Cancelled;
                    _db.ResourceBookings.Add(new ResourceBooking
                    {
                        MissionId = missionId,
                        DroneId = droneId,
                        StartAt = plannedStart.Value,
                        EndAt = plannedEnd.Value,
                        Status = ResourceBookingStatus.Active
                    });
                }
            }
            else
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
        }
        else
        {
            if (await _db.Missions.AnyAsync(x => x.Id != missionId && x.UavId == droneId && x.Status != MissionStatus.Completed && x.Status != MissionStatus.Cancelled, ct))
                throw new BusinessRuleException("DRONE_ALREADY_RESERVED");
        }

        var replaced = mission.UavId != Guid.Empty && mission.UavId != droneId; mission.UavId = droneId; mission.RecalculateReadiness();
        Audit(mission.Id, replaced ? "DRONE_REPLACED" : "DRONE_ASSIGNED"); await _db.SaveChangesAsync(ct);
    }

    public async Task<DroneHandover> ConfirmHandoverAsync(Guid missionId, Mf01Handover request, CancellationToken ct)
    {
        var mission = await AccessibleMission(missionId, ct, true);
        if (mission.Status is not (MissionStatus.Assigned or MissionStatus.Preparing)) throw new BusinessRuleException("HANDOVER_INVALID_STATE");
        if (mission.UavId != request.DroneId) throw new BusinessRuleException("DRONE_NOT_ASSIGNED");
        if (!mission.Assignments.Any(x => x.UserId == request.ReceivedBy && x.Status == MissionAssignmentStatus.Active)) throw new ForbiddenException("RECEIVER_NOT_ASSIGNED");
        if (string.IsNullOrWhiteSpace(request.Condition)) throw new BusinessRuleException("DRONE_CONDITION_REQUIRED");
        if (mission.DroneHandovers.Any(x => x.DroneId == request.DroneId && x.ReturnedAt == null)) throw new BusinessRuleException("DUPLICATE_HANDOVER");
        var handover = new DroneHandover { MissionId = missionId, DroneId = request.DroneId, HandedOverBy = _current.UserId,
            ReceivedBy = request.ReceivedBy, ReceivedAt = DateTime.UtcNow, Condition = request.Condition,
            Status = request.Accepted ? DroneHandoverStatus.Accepted : DroneHandoverStatus.Rejected };
        _db.DroneHandovers.Add(handover); mission.DroneHandovers.Add(handover); mission.RecalculateReadiness(); Audit(mission.Id, "DRONE_HANDOVER_CONFIRMED");
        await _db.SaveChangesAsync(ct); return handover;
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

        if (mission.CheckIns.Any(x => x.UserId == _current.UserId && x.Status == MissionCheckInStatus.CheckedIn))
            throw new BusinessRuleException("DUPLICATE_CHECK_IN");

        var checkIn = new MissionCheckIn { MissionId = missionId, UserId = _current.UserId, CheckedInAt = DateTime.UtcNow };
        _db.MissionCheckIns.Add(checkIn);
        mission.CheckIns.Add(checkIn);
        var ready = mission.RecalculateReadiness();
        Audit(mission.Id, "CHECK_IN");
        if (ready) Audit(mission.Id, "MISSION_READY");
        await _db.SaveChangesAsync(ct);
        return checkIn;
    }

    public async Task<MissionAssignment> AcceptAssignmentAsync(Guid missionId, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        var mission = await AccessibleMission(missionId, ct, true);
        var assignment = mission.Assignments.SingleOrDefault(x => x.UserId == _current.UserId && x.Status == MissionAssignmentStatus.Active)
            ?? throw new NotFoundException("MissionAssignment", _current.UserId);

        if (assignment.ResponseStatus == MissionAssignmentResponse.Accepted)
            return assignment;

        assignment.ResponseStatus = MissionAssignmentResponse.Accepted;
        assignment.RespondedAt = DateTime.UtcNow;
        assignment.Version++;

        // H6: Always update mission version to ensure atomic optimistic concurrency token check
        mission.Version++;
        mission.UpdatedAt = DateTime.UtcNow;

        var allConfirmed = false;
        if (mission.Status == MissionStatus.PendingAcceptance)
        {
            allConfirmed = mission.CheckAcceptance();
            if (allConfirmed)
            {
                Audit(mission.Id, "MISSION_CONFIRMED");
            }
        }
        else
        {
            mission.RecalculateReadiness();
        }

        Audit(mission.Id, "ASSIGNMENT_ACCEPTED");
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
                InspectorId = mission.InspectorId?.ToString() ?? string.Empty,
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
        var assignment = mission.Assignments.SingleOrDefault(x => x.UserId == _current.UserId && x.Status == MissionAssignmentStatus.Active)
            ?? throw new NotFoundException("MissionAssignment", _current.UserId);

        assignment.ResponseStatus = MissionAssignmentResponse.Postponed;
        assignment.ResponseReason = reason;
        assignment.RespondedAt = DateTime.UtcNow;
        assignment.Version++;

        mission.PostponedAt = DateTime.UtcNow;
        mission.PostponeReason = reason;
        mission.Version++;

        Audit(mission.Id, "ASSIGNMENT_POSTPONED");
        Notify(mission.ManagerId, mission, "ASSIGNMENT_POSTPONED");
        await _db.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task StartAsync(Guid missionId, CancellationToken ct)
    {
        var m = await AccessibleMission(missionId, ct, true);
        EnsureAssignedInspectorOrManager(m);
        try
        {
            m.Start();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException("INVALID_MISSION_STATE", ex.Message);
        }

        if (m.UavId.HasValue && m.UavId.Value != Guid.Empty)
        {
            var drone = await _db.Uavs.FirstOrDefaultAsync(d => d.Id == m.UavId.Value, ct);
            if (drone != null)
            {
                drone.Status = DroneStatus.Flying;
                drone.UpdatedAt = DateTime.UtcNow;
            }
        }

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
                InspectorId = m.InspectorId?.ToString() ?? string.Empty,
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

        if (m.UavId.HasValue && m.UavId.Value != Guid.Empty)
        {
            var drone = await _db.Uavs.FirstOrDefaultAsync(d => d.Id == m.UavId.Value, ct);
            if (drone != null && drone.Status == DroneStatus.Flying)
            {
                drone.Status = DroneStatus.Idle;
                drone.UpdatedAt = DateTime.UtcNow;
            }
        }

        var activeBookings = await _db.ResourceBookings
            .Where(b => b.MissionId == m.Id && b.Status == ResourceBookingStatus.Active)
            .ToListAsync(ct);
        foreach (var b in activeBookings)
        {
            b.Status = ResourceBookingStatus.Released;
            b.UpdatedAt = DateTime.UtcNow;
        }

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
                InspectorId = m.InspectorId?.ToString() ?? string.Empty,
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
        try
        {
            mission.Confirm(reason);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException("INVALID_MISSION_STATE", ex.Message);
        }

        foreach (var a in mission.Assignments.Where(x => x.UserId == _current.UserId && x.Status == MissionAssignmentStatus.Active))
        {
            a.ResponseStatus = MissionAssignmentResponse.Accepted;
            a.RespondedAt = DateTime.UtcNow;
            a.Version++;
        }

        Audit(mission.Id, "MISSION_CONFIRMED");
        if (mission.ManagerId != Guid.Empty)
        {
            Notify(mission.ManagerId, mission, "MISSION_CONFIRMED");
        }

        var actorName = _current.Username ?? "Phi công";
        var commContent = string.IsNullOrWhiteSpace(reason)
            ? "Phi công đã xác nhận tiếp nhận sẵn sàng bay."
            : $"Phi công đã xác nhận tiếp nhận: {reason}";

        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            SenderName = actorName,
            SenderRole = "INSPECTOR",
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
                InspectorId = mission.InspectorId?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = new UavPms.Shared.Contracts.Events.MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = _current.UserId.ToString(),
                    SenderName = commLog.SenderName,
                    SenderRole = commLog.SenderRole,
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
        if (mission.InspectorId != Guid.Empty)
        {
            Notify(mission.InspectorId ?? Guid.Empty, mission, "MISSION_SUSPENDED");
        }
        foreach (var a in mission.Assignments.Where(x => x.Status == MissionAssignmentStatus.Active))
        {
            Notify(a.UserId, mission, "MISSION_SUSPENDED");
        }

        var actorName = _current.Username ?? "Quản lý";
        var commContent = string.IsNullOrWhiteSpace(reason)
            ? "Quản lý đã tạm đình chỉ bay khẩn cấp."
            : $"Quản lý đã tạm đình chỉ bay khẩn cấp: {reason}";

        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            SenderName = actorName,
            SenderRole = "MANAGER",
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
                InspectorId = mission.InspectorId?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = new UavPms.Shared.Contracts.Events.MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = _current.UserId.ToString(),
                    SenderName = commLog.SenderName,
                    SenderRole = commLog.SenderRole,
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
        if (mission.InspectorId != Guid.Empty)
        {
            Notify(mission.InspectorId ?? Guid.Empty, mission, "MISSION_RESUMED");
        }
        foreach (var a in mission.Assignments.Where(x => x.Status == MissionAssignmentStatus.Active))
        {
            Notify(a.UserId, mission, "MISSION_RESUMED");
        }

        var actorName = _current.Username ?? "Quản lý";
        var commContent = string.IsNullOrWhiteSpace(reason)
            ? "Quản lý đã dỡ lệnh tạm đình chỉ bay."
            : $"Quản lý đã dỡ lệnh tạm đình chỉ bay: {reason}";

        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            SenderName = actorName,
            SenderRole = "MANAGER",
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
                Status = "CONFIRMED",
                ActorId = _current.UserId.ToString(),
                ActorName = actorName,
                ActorRole = "MANAGER",
                Reason = commContent,
                ManagerId = mission.ManagerId.ToString(),
                InspectorId = mission.InspectorId?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = new UavPms.Shared.Contracts.Events.MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = _current.UserId.ToString(),
                    SenderName = commLog.SenderName,
                    SenderRole = commLog.SenderRole,
                    Type = commLog.Type,
                    Content = commLog.Content,
                    Timestamp = commLog.CreatedAt
                }
            };
            await _notifier.NotifyAsync(eventDto, ct);
        }

        return mission;
    }

    public async Task<Mission> PostponeMissionAsync(Guid missionId, string reason, CancellationToken ct)
    {
        await RequireActiveCaller(ct);
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("POSTPONE_REASON_REQUIRED", "A reason must be provided when postponing an assignment.");

        var mission = await AccessibleMission(missionId, ct, true);

        try
        {
            mission.Postpone(reason);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException("INVALID_MISSION_STATE", ex.Message);
        }

        foreach (var a in mission.Assignments.Where(x => x.UserId == _current.UserId && x.Status == MissionAssignmentStatus.Active))
        {
            a.ResponseStatus = MissionAssignmentResponse.Postponed;
            a.ResponseReason = reason;
            a.RespondedAt = DateTime.UtcNow;
            a.Version++;
        }

        Audit(mission.Id, "MISSION_POSTPONED");
        if (mission.ManagerId != Guid.Empty)
        {
            Notify(mission.ManagerId, mission, "MISSION_POSTPONED");
        }

        var actorName = _current.Username ?? "Phi công";
        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            SenderName = actorName,
            SenderRole = "INSPECTOR",
            Type = "POSTPONE",
            Content = $"Phi công xin hoãn nhiệm vụ: {reason}",
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
                ActorRole = "INSPECTOR",
                Reason = reason,
                ManagerId = mission.ManagerId.ToString(),
                InspectorId = mission.InspectorId?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = new UavPms.Shared.Contracts.Events.MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = _current.UserId.ToString(),
                    SenderName = commLog.SenderName,
                    SenderRole = commLog.SenderRole,
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

        var bookings = await _db.ResourceBookings
            .Where(b => b.MissionId == missionId && b.Status == ResourceBookingStatus.Active)
            .ToListAsync(ct);
        foreach (var b in bookings)
        {
            b.Status = ResourceBookingStatus.Cancelled;
        }

        Audit(mission.Id, "MISSION_CANCELLED");
        foreach (var a in mission.Assignments.Where(x => x.Status == MissionAssignmentStatus.Active))
            Notify(a.UserId, mission, "MISSION_CANCELLED");

        var actorName = _current.Username ?? "Quản lý";
        var commContent = string.IsNullOrWhiteSpace(reason)
            ? "Quản lý đã hủy bỏ nhiệm vụ."
            : $"Quản lý đã hủy bỏ nhiệm vụ: {reason}";

        var commLog = new MissionCommunicationLog
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            SenderId = _current.UserId,
            SenderName = actorName,
            SenderRole = "MANAGER",
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
                InspectorId = mission.InspectorId?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = new UavPms.Shared.Contracts.Events.MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = _current.UserId.ToString(),
                    SenderName = commLog.SenderName,
                    SenderRole = commLog.SenderRole,
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
        var inspectorId = (mission.InspectorId.HasValue && mission.InspectorId.Value != Guid.Empty)
            ? mission.InspectorId.Value
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
            SenderName = actorName,
            SenderRole = "MANAGER",
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
                    SenderName = commLog.SenderName,
                    SenderRole = commLog.SenderRole,
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
            SenderName = actorName,
            SenderRole = senderRole,
            Type = "MESSAGE",
            Content = message,
            CreatedAt = DateTime.UtcNow
        };
        _db.MissionCommunicationLogs.Add(commLog);

        var recipientId = isManager
            ? ((mission.InspectorId.HasValue && mission.InspectorId.Value != Guid.Empty) ? mission.InspectorId.Value : mission.AssignedToUserId)
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
            SenderName = commLog.SenderName,
            SenderRole = commLog.SenderRole,
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
                InspectorId = mission.InspectorId?.ToString() ?? string.Empty,
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
                SenderName = x.SenderName,
                SenderRole = x.SenderRole,
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
            .Include(a => a.Asset)
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
                TimestampSeconds = a.Timestamp,
                TimestampLabel = a.Timestamp.HasValue ? TimeSpan.FromSeconds(a.Timestamp.Value).ToString(@"mm\:ss") : null,
                FrameIndex = a.FrameIndex,
                ImageUrl = !string.IsNullOrWhiteSpace(a.ImageUrl) ? a.ImageUrl : (!string.IsNullOrWhiteSpace(a.CropUrl) ? a.CropUrl : media?.FileUrl),
                SourceUrl = media?.FileUrl,
                AssetId = a.AssetId?.ToString(),
                Tower = a.TowerId ?? "Cột chưa xác định",
                Gps = a.Gps,
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

        anomaly.ValidationStatus = isApproved ? "Confirmed" : "Rejected";
        anomaly.AnalystId = _current.UserId;
        anomaly.AnalystNotes = request.ReviewNotes ?? string.Empty;
        anomaly.ValidatedAt = DateTime.UtcNow;

        string? createdTaskId = null;
        double? newHealthScore = null;

        if (isApproved)
        {
            // 1. Cập nhật giảm điểm sức khỏe của thiết bị (Asset Health Score)
            if (anomaly.AssetId.HasValue && anomaly.AssetId.Value != Guid.Empty)
            {
                var asset = await _db.Assets.SingleOrDefaultAsync(x => x.Id == anomaly.AssetId.Value, ct);
                if (asset != null)
                {
                    double penalty = 15;
                    if (string.Equals(request.OverrideSeverity, "Critical Risk", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(request.OverrideSeverity, "Urgent", StringComparison.OrdinalIgnoreCase))
                    {
                        penalty = 25;
                    }
                    else if ((anomaly.Category?.SeverityWeight ?? 0) > 0)
                    {
                        penalty = Math.Min(30, anomaly.Category!.SeverityWeight * 3);
                    }

                    asset.CurrentHealthScore = Math.Max(0, Math.Round(asset.CurrentHealthScore - penalty, 1));
                    asset.LastInspectedAt = DateTime.UtcNow;
                    newHealthScore = asset.CurrentHealthScore;

                    var healthHistory = new AssetHealthHistory
                    {
                        Id = Guid.NewGuid(),
                        AssetId = asset.Id,
                        HealthScore = asset.CurrentHealthScore,
                        CalculatedAt = DateTime.UtcNow,
                        CalculationLog = $"{{\"penalty\": {penalty}, \"action\": \"AI_DETECTION_REVIEW\", \"notes\": \"{request.ReviewNotes}\"}}",
                        RiskLevel = asset.CurrentHealthScore < 40 ? "Critical" : (asset.CurrentHealthScore < 70 ? "Medium" : "Low")
                    };
                    _db.AssetHealthHistories.Add(healthHistory);
                }
            }

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
                    AssetId = anomaly.AssetId ?? Guid.Empty,
                    ManagerId = mission.ManagerId,
                    TechnicianId = techAssignment?.UserId ?? Guid.Empty,
                    Status = TicketStatus.Open,
                    Priority = priority,
                    Description = $"[Khuyến nghị từ Giám định AI] {anomaly.Category?.CategoryName ?? "Khuyết tật đường dây"}. {request.ReviewNotes ?? anomaly.AnalystNotes}",
                    AssignedAt = DateTime.UtcNow,
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
            .Include(t => t.Asset)
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
                TicketStatus.InProgress or TicketStatus.PendingVerification => "InProgress",
                TicketStatus.Resolved or TicketStatus.Closed => "Completed",
                _ => "Pending"
            };

            var catName = t.Anomaly?.Category?.CategoryName ?? "Khuyết tật thiết bị";
            var assetCode = t.Asset?.AssetCode ?? "INS-220KV-042-PHA-B";
            var towerCode = t.Anomaly?.TowerId ?? "Cột 042";

            results.Add(new MissionMaintenanceTaskDto
            {
                Id = t.Id.ToString(),
                MissionId = missionId.ToString(),
                DetectionId = t.AnomalyId.ToString(),
                Title = $"Khắc phục & sửa chữa {catName}",
                Priority = priorityStr,
                TowerCode = towerCode,
                AssetCode = assetCode,
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
                SenderName = x.SenderName,
                SenderRole = x.SenderRole,
                Content = x.Content,
                Timestamp = x.CreatedAt
            })
            .ToListAsync(ct);

        return logs;
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
            SenderName = actorName,
            SenderRole = senderRole,
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
            SenderName = commLog.SenderName,
            SenderRole = commLog.SenderRole,
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
                InspectorId = mission.InspectorId?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                Log = new MissionCommunicationLogDto
                {
                    Id = commLog.Id.ToString(),
                    SenderId = (commLog.SenderId ?? Guid.Empty).ToString(),
                    SenderName = commLog.SenderName,
                    SenderRole = commLog.SenderRole,
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
        if (drone != null)
        {
            drone.Status = DroneStatus.Idle;
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

        Guid resolvedAssetId = Guid.Empty;
        if (request.AssetId.HasValue && request.AssetId.Value != Guid.Empty)
        {
            var assetExists = await _db.Assets.AnyAsync(a => a.Id == request.AssetId.Value, ct);
            if (!assetExists) throw new NotFoundException("Asset", request.AssetId.Value);
            resolvedAssetId = request.AssetId.Value;
        }
        else if (mission.MissionTargets.Count > 0)
        {
            resolvedAssetId = mission.MissionTargets.First().AssetId;
        }
        else
        {
            var firstAsset = await _db.Assets.FirstOrDefaultAsync(a => !a.IsDeleted && (mission.RegionId == null || a.Tower != null), ct);
            if (firstAsset != null) resolvedAssetId = firstAsset.Id;
        }

        if (resolvedAssetId == Guid.Empty)
            throw new BusinessRuleException("ASSET_REQUIRED", "An AssetId is required to associate the incident report.");

        var isCritical = request.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase);

        var incident = new IncidentReport
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            ReportedBy = _current.UserId,
            AssetId = resolvedAssetId,
            IncidentType = request.IncidentType,
            Severity = char.ToUpperInvariant(request.Severity[0]) + request.Severity[1..].ToLowerInvariant(),
            Description = request.Description.Trim(),
            FileUrl = request.FileUrl ?? string.Empty,
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
            .Include(x => x.Asset)
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

    private IQueryable<Asset> AssetsForRegion(Guid regionId) => _db.Assets.Where(x => (x.Status == "Active" || x.Status == "Operational") && x.Tower!.TransmissionLine!.Substation!.RegionAssetId == regionId);
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
    private async Task<Mission> ManagedMission(Guid id, CancellationToken ct, bool graph = false) { await RequireManageMission(id, ct); return await MissionQuery(graph).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Mission", id); }
    private async Task<Mission> AccessibleMission(Guid id, CancellationToken ct, bool graph = false)
    {
        await RequireActiveCaller(ct);
        var global = IsGlobal;
        var uid = _current.UserId;
        var userRegionScopes = await _db.UserGeographicScopes
            .Where(x => x.UserId == uid)
            .Select(x => x.RegionId)
            .ToListAsync(ct);

        var m = await MissionQuery(graph).SingleOrDefaultAsync(x => x.Id == id && (
            global ||
            x.ManagerId == uid ||
            (x.InspectorId.HasValue && x.InspectorId.Value == uid) ||
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
    private static void RequirePreExecution(Mission m) { if (m.Status is MissionStatus.InProgress or MissionStatus.Completed or MissionStatus.Cancelled) throw new BusinessRuleException("MISSION_IMMUTABLE_AFTER_START"); }
    private void EnsureAssignedInspectorOrManager(Mission m)
    {
        if (IsGlobal || _current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase))
            return;

        var uid = _current.UserId;
        var isAssigned = (m.InspectorId.HasValue && m.InspectorId.Value == uid) ||
                         m.Assignments.Any(a => a.UserId == uid && a.Status == MissionAssignmentStatus.Active);

        if (!isAssigned)
            throw new ForbiddenException("USER_NOT_ASSIGNED_TO_MISSION");
    }
    private void Audit(Guid id, string action, string oldValues = "{}", string newValues = "{}") => _db.AuditLogs.Add(new AuditLog { UserId = _current.UserId, TableName = "Missions", RecordId = id, ActionType = action, OldValues = oldValues, NewValues = newValues, IpAddress = _current.IpAddress ?? "", UserAgent = _current.UserAgent ?? "" });
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
            IsRead = false,
            SentAt = DateTime.UtcNow
        });
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
    }
}
