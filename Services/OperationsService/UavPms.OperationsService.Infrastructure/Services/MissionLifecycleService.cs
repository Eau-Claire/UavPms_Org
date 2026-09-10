using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class MissionLifecycleService : IMissionLifecycleService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _current;
    public MissionLifecycleService(ApplicationDbContext db, ICurrentUserServices current) { _db = db; _current = current; }

    public async Task<Mission> CreateAsync(Mf01CreateMission request, CancellationToken ct)
    {
        await RequireActiveCaller(ct); await RequireManageRegion(request.RegionId, ct);
        var region = await _db.Regions.SingleOrDefaultAsync(x => x.Id == request.RegionId, ct)
            ?? throw new NotFoundException("Region", request.RegionId);
        if (region.IsDeleted) throw new BusinessRuleException("REGION_INACTIVE");
        if (request.PlannedStart >= request.PlannedEnd) throw new BusinessRuleException("INVALID_PLANNED_TIME");
        InspectionSchedule? schedule = null;
        if (request.MissionType == MissionType.Scheduled)
        {
            if (request.ScheduleId is null) throw new BusinessRuleException("SCHEDULE_REQUIRED");
            schedule = await _db.InspectionSchedules.SingleOrDefaultAsync(x => x.Id == request.ScheduleId, ct)
                ?? throw new NotFoundException("InspectionSchedule", request.ScheduleId.Value);
            if (!schedule.IsActive || schedule.RegionId != request.RegionId) throw new BusinessRuleException("SCHEDULE_REGION_MISMATCH");
        }
        else if (request.ScheduleId is not null) throw new BusinessRuleException("AD_HOC_SCHEDULE_NOT_ALLOWED");

        var mission = new Mission { MissionCode = $"MS-{DateTime.UtcNow:yyyyMMddHHmmssfff}", Title = request.Title,
            RegionId = request.RegionId, ScheduleId = request.ScheduleId, MissionType = request.MissionType,
            TriggerReason = request.TriggerReason, PlannedStart = request.PlannedStart, PlannedEnd = request.PlannedEnd,
            ScheduledStartAt = request.PlannedStart, Description = request.Description ?? string.Empty,
            ManagerId = _current.UserId, Status = MissionStatus.Draft };
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.Missions.Add(mission); Audit(mission.Id, "MISSION_CREATED");
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return mission;
    }

    public async Task<IReadOnlyList<Asset>> ResolveScopeAsync(Guid missionId, string boundaryWkt, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct); var boundary = ParseBoundary(boundaryWkt);
        return await AssetsForRegion(mission.RegionId).Where(x => x.Location != null && boundary.Covers(x.Location)).ToListAsync(ct);
    }

    public async Task ConfirmAssetsAsync(Guid missionId, string boundaryWkt, IReadOnlyCollection<Guid> assetIds, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct); RequirePreExecution(mission);
        if (assetIds.Count == 0) throw new BusinessRuleException("MISSION_TARGET_REQUIRED");
        var distinct = assetIds.Distinct().ToArray(); if (distinct.Length != assetIds.Count) throw new BusinessRuleException("DUPLICATE_ASSET");
        var boundary = ParseBoundary(boundaryWkt);
        var assets = await AssetsForRegion(mission.RegionId).Where(x => distinct.Contains(x.Id)).ToListAsync(ct);
        if (assets.Count != distinct.Length) throw new ForbiddenException("ASSET_OUTSIDE_REGION_OR_SCOPE");
        if (assets.Any(x => x.Location == null || !boundary.Covers(x.Location))) throw new BusinessRuleException("ASSET_OUTSIDE_BOUNDARY");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.MissionTargets.RemoveRange(await _db.MissionTargets.Where(x => x.MissionId == mission.Id).ToListAsync(ct));
        _db.MissionTargets.AddRange(distinct.Select((id, i) => new MissionTarget { MissionId = mission.Id, AssetId = id, Sequence = i + 1 }));
        mission.Boundary = boundary; Audit(mission.Id, "MISSION_SCOPE_CHANGED"); Audit(mission.Id, "MISSION_ASSETS_CHANGED");
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<MissionAssignment> AssignAsync(Guid missionId, Mf01Assignment request, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct, true); RequirePreExecution(mission);
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == request.UserId, ct) ?? throw new NotFoundException("User", request.UserId);
        if (!IsActive(user.Status)) throw new BusinessRuleException("ASSIGNEE_INACTIVE");
        if (await _db.MissionAssignments.AnyAsync(x => x.MissionId == missionId && x.UserId == request.UserId && x.Status == MissionAssignmentStatus.Active, ct))
            throw new BusinessRuleException("DUPLICATE_ASSIGNMENT");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var assignment = new MissionAssignment { MissionId = missionId, UserId = request.UserId, AssignmentRole = request.AssignmentRole, AssignedByUserId = _current.UserId };
        _db.MissionAssignments.Add(assignment); mission.Assignments.Add(assignment); mission.RecalculateReadiness();
        Notify(request.UserId, mission, "MISSION_ASSIGNED"); Audit(mission.Id, "MISSION_ASSIGNMENT_ADDED");
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return assignment;
    }

    public async Task RemoveAssignmentAsync(Guid missionId, Guid assignmentId, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct, true); RequirePreExecution(mission);
        var assignment = mission.Assignments.SingleOrDefault(x => x.Id == assignmentId && x.Status == MissionAssignmentStatus.Active)
            ?? throw new NotFoundException("MissionAssignment", assignmentId);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        assignment.Status = MissionAssignmentStatus.Revoked; assignment.EndedAt = DateTime.UtcNow; mission.RecalculateReadiness();
        Notify(assignment.UserId, mission, "MISSION_ASSIGNMENT_REMOVED"); Audit(mission.Id, "MISSION_ASSIGNMENT_REMOVED");
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task AssignDroneAsync(Guid missionId, Guid droneId, CancellationToken ct)
    {
        var mission = await ManagedMission(missionId, ct, true); RequirePreExecution(mission);
        var drone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == droneId, ct) ?? throw new NotFoundException("Drone", droneId);
        if (drone.Status != DroneStatus.Idle) throw new BusinessRuleException("DRONE_UNAVAILABLE");
        if (await _db.Missions.AnyAsync(x => x.Id != missionId && x.UavId == droneId && x.Status != MissionStatus.Completed && x.Status != MissionStatus.Cancelled, ct))
            throw new BusinessRuleException("DRONE_ALREADY_RESERVED");
        var replaced = mission.UavId != Guid.Empty; mission.UavId = droneId; mission.RecalculateReadiness();
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
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var handover = new DroneHandover { MissionId = missionId, DroneId = request.DroneId, HandedOverBy = _current.UserId,
            ReceivedBy = request.ReceivedBy, ReceivedAt = DateTime.UtcNow, Condition = request.Condition,
            Status = request.Accepted ? DroneHandoverStatus.Accepted : DroneHandoverStatus.Rejected };
        _db.DroneHandovers.Add(handover); mission.DroneHandovers.Add(handover); mission.RecalculateReadiness(); Audit(mission.Id, "DRONE_HANDOVER_CONFIRMED");
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return handover;
    }

    public async Task<MissionCheckIn> CheckInAsync(Guid missionId, CancellationToken ct)
    {
        await RequireActiveCaller(ct); var mission = await AccessibleMission(missionId, ct, true);
        if (mission.Status is not (MissionStatus.Assigned or MissionStatus.Preparing)) throw new BusinessRuleException("CHECK_IN_INVALID_STATE");
        if (!mission.Assignments.Any(x => x.UserId == _current.UserId && x.Status == MissionAssignmentStatus.Active)) throw new ForbiddenException("USER_NOT_ASSIGNED");
        if (mission.CheckIns.Any(x => x.UserId == _current.UserId && x.Status == MissionCheckInStatus.CheckedIn)) throw new BusinessRuleException("DUPLICATE_CHECK_IN");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var checkIn = new MissionCheckIn { MissionId = missionId, UserId = _current.UserId, CheckedInAt = DateTime.UtcNow };
        _db.MissionCheckIns.Add(checkIn); mission.CheckIns.Add(checkIn); var ready = mission.RecalculateReadiness(); Audit(mission.Id, "CHECK_IN"); if (ready) Audit(mission.Id, "MISSION_READY");
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return checkIn;
    }

    public async Task StartAsync(Guid missionId, CancellationToken ct) { var m = await AccessibleMission(missionId, ct, true); m.Start(); m.Version++; Audit(m.Id, "MISSION_STARTED"); await SaveConcurrency(ct); }
    public async Task CompleteAsync(Guid missionId, CancellationToken ct) { var m = await AccessibleMission(missionId, ct, true); m.Complete(); m.Version++; Audit(m.Id, "MISSION_COMPLETED"); await SaveConcurrency(ct); }
    public async Task CancelAsync(Guid missionId, CancellationToken ct) { var m = await ManagedMission(missionId, ct, true); m.Cancel(); m.Version++; Audit(m.Id, "MISSION_CANCELLED"); foreach (var a in m.Assignments.Where(x => x.Status == MissionAssignmentStatus.Active)) Notify(a.UserId, m, "MISSION_CANCELLED"); await SaveConcurrency(ct); }

    private IQueryable<Asset> AssetsForRegion(Guid regionId) => _db.Assets.Where(x => (x.Status == "Active" || x.Status == "Operational") && x.Tower!.TransmissionLine!.Substation!.RegionAssetId == regionId);
    private static Geometry ParseBoundary(string wkt) { try { var g = new WKTReader().Read(wkt); if (!g.IsValid || g.IsEmpty || g is not (Polygon or MultiPolygon)) throw new Exception(); g.SRID = 4326; return g; } catch { throw new BusinessRuleException("INVALID_GEOMETRY"); } }
    private async Task<Mission> ManagedMission(Guid id, CancellationToken ct, bool graph = false) { await RequireManageMission(id, ct); return await MissionQuery(graph).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Mission", id); }
    private async Task<Mission> AccessibleMission(Guid id, CancellationToken ct, bool graph = false) { await RequireActiveCaller(ct); var global = IsGlobal; var uid = _current.UserId; var m = await MissionQuery(graph).SingleOrDefaultAsync(x => x.Id == id && (global || x.ManagerId == uid || x.Assignments.Any(a => a.UserId == uid && a.Status == MissionAssignmentStatus.Active)), ct); return m ?? throw new ForbiddenException("MISSION_ACCESS_DENIED"); }
    private IQueryable<Mission> MissionQuery(bool graph) { var q = _db.Missions.AsQueryable(); return graph ? q.Include(x => x.Assignments).Include(x => x.CheckIns).Include(x => x.DroneHandovers).Include(x => x.MissionTargets) : q; }
    private async Task RequireManageMission(Guid id, CancellationToken ct) { var region = await _db.Missions.Where(x => x.Id == id).Select(x => (Guid?)x.RegionId).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Mission", id); await RequireManageRegion(region, ct); }
    private async Task RequireManageRegion(Guid region, CancellationToken ct) { if (IsGlobal) return; if (!_current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase) || !await _db.UserGeographicScopes.AnyAsync(x => x.UserId == _current.UserId && x.RegionId == region, ct)) throw new ForbiddenException("REGION_MANAGEMENT_SCOPE_REQUIRED"); }
    private async Task RequireActiveCaller(CancellationToken ct) { if (!_current.IsAuthenticated || _current.UserId == Guid.Empty) throw new ForbiddenException("AUTHENTICATION_REQUIRED"); var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == _current.UserId, ct); if (user == null || !IsActive(user.Status)) throw new ForbiddenException("ACTIVE_USER_REQUIRED"); }
    private bool IsGlobal => _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase);
    private static bool IsActive(string status) => status.Equals("Active", StringComparison.OrdinalIgnoreCase) || status.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
    private static void RequirePreExecution(Mission m) { if (m.Status is MissionStatus.InProgress or MissionStatus.Completed or MissionStatus.Cancelled) throw new BusinessRuleException("MISSION_IMMUTABLE_AFTER_START"); }
    private void Audit(Guid id, string action) => _db.AuditLogs.Add(new AuditLog { UserId = _current.UserId, TableName = "Missions", RecordId = id, ActionType = action, OldValues = "{}", NewValues = "{}", IpAddress = _current.IpAddress ?? "", UserAgent = _current.UserAgent ?? "" });
    private void Notify(Guid userId, Mission m, string type) => _db.Notifications.Add(new Notification { UserId = userId, Type = type, ReferenceType = "Mission", ReferenceId = m.Id, Title = m.Title, Body = type });
    private async Task SaveConcurrency(CancellationToken ct) { try { await _db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new BusinessRuleException("MISSION_CONCURRENCY_CONFLICT"); } }
}
