using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class PreMissionAssessmentService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _current;
    public PreMissionAssessmentService(ApplicationDbContext db, ICurrentUserServices current) { _db = db; _current = current; }

    public async Task<PreMissionAssessment> CreateAsync(Guid regionId, DateTime plannedStart, DateTime plannedEnd, IReadOnlyCollection<Guid> assetIds, CancellationToken ct)
    {
        await RequireManager(ct);
        if (plannedEnd <= plannedStart || plannedEnd <= DateTime.UtcNow) throw new BusinessRuleException("INVALID_PLANNED_TIME");
        if (assetIds.Count == 0 || assetIds.Count != assetIds.Distinct().Count()) throw new BusinessRuleException("INVALID_PROPOSED_SCOPE");
        if (!await _db.Regions.AnyAsync(x => x.Id == regionId, ct)) throw new NotFoundException("Region", regionId);
        if (!_current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase) && !await _db.UserGeographicScopes.AnyAsync(x => x.UserId == _current.UserId && x.RegionId == regionId, ct)) throw new ForbiddenException("REGION_MANAGEMENT_SCOPE_REQUIRED");
        var assets = await _db.Assets.Where(x => assetIds.Contains(x.Id) && (x.Status == "Active" || x.Status == "Operational") && x.Tower!.TransmissionLine!.Substation!.RegionAssetId == regionId).ToListAsync(ct);
        if (assets.Count != assetIds.Count) throw new ForbiddenException("ASSET_OUTSIDE_MANAGEMENT_SCOPE");
        var assessment = new PreMissionAssessment { ManagerId = _current.UserId, RegionId = regionId, PlannedStart = plannedStart, PlannedEnd = plannedEnd, Status = PreMissionAssessmentStatus.Evaluating };
        assessment.Assets = assetIds.Select((id, i) => new PreMissionAssessmentAsset { Assessment = assessment, AssetId = id, Sequence = i + 1 }).ToList();
        _db.PreMissionAssessments.Add(assessment);
        await _db.SaveChangesAsync(ct);
        return assessment;
    }

    public async Task<PreMissionAssessment> EvaluateAsync(Guid id, CancellationToken ct)
    {
        await RequireManager(ct);
        var assessment = await _db.PreMissionAssessments.Include(x => x.Assets).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("PreMissionAssessment", id);
        if (assessment.ManagerId != _current.UserId && !_current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase)) throw new ForbiddenException("ASSESSMENT_ACCESS_DENIED");
        var activeUsers = await _db.Users.Where(x => x.IsEmailVerified && (x.Status == "Active" || x.Status == "Enabled")).ToListAsync(ct);
        foreach (var user in activeUsers.Where(x => x.UserRoles.Any(r => r.Role != null && (r.Role.RoleName == UserRoles.Inspector || r.Role.RoleName == UserRoles.Pilot))))
            if (!assessment.PersonnelCandidates.Any(x => x.UserId == user.Id)) assessment.PersonnelCandidates.Add(new PreMissionAssessmentPersonnel { UserId = user.Id, IsEligible = true });
        var drones = await _db.Uavs.Include(x => x.TechnicalInspections).Where(x => x.OperationalStatus == DroneOperationalStatus.Available && !x.IsDeleted).ToListAsync(ct);
        foreach (var drone in drones)
        {
            var inspection = drone.TechnicalInspections.Where(x => x.Status == DroneTechnicalInspectionStatus.Passed && x.ValidUntil > DateTime.UtcNow).OrderByDescending(x => x.CompletedAt).FirstOrDefault();
            assessment.DroneCandidates.Add(new PreMissionAssessmentDrone { DroneId = drone.Id, IsEligible = inspection != null && inspection.Health is TechnicalHealth.Healthy or TechnicalHealth.Warning, TechnicalHealth = inspection?.Health ?? TechnicalHealth.Unknown, TechnicalInspectionId = inspection?.Id });
        }
        var ready = assessment.Assets.Count > 0 && assessment.PersonnelCandidates.Any(x => x.IsEligible) && assessment.DroneCandidates.Any(x => x.IsEligible);
        assessment.Status = ready ? PreMissionAssessmentStatus.Ready : PreMissionAssessmentStatus.NotReady;
        assessment.ValidUntil = DateTime.UtcNow.AddHours(4);
        assessment.Version++;
        await _db.SaveChangesAsync(ct);
        return assessment;
    }

    public async Task<Mission> CreateMissionAsync(Guid assessmentId, string title, Guid inspectorId, Guid droneId, CancellationToken ct)
    {
        await RequireManager(ct);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var assessment = await _db.PreMissionAssessments.Include(x => x.Assets).Include(x => x.PersonnelCandidates).Include(x => x.DroneCandidates).SingleOrDefaultAsync(x => x.Id == assessmentId, ct) ?? throw new NotFoundException("PreMissionAssessment", assessmentId);
        if (assessment.Status != PreMissionAssessmentStatus.Ready || assessment.ValidUntil <= DateTime.UtcNow) throw new BusinessRuleException("ASSESSMENT_EXPIRED_OR_NOT_READY");
        if (assessment.ManagerId != _current.UserId && !_current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase)) throw new ForbiddenException("ASSESSMENT_ACCESS_DENIED");
        if (assessment.DroneCandidates.All(x => x.DroneId != droneId || !x.IsEligible) || assessment.PersonnelCandidates.All(x => x.UserId != inspectorId || !x.IsEligible)) throw new BusinessRuleException("RESOURCE_NOT_IN_ASSESSMENT");
        var drone = await _db.Uavs.Include(x => x.TechnicalInspections).SingleOrDefaultAsync(x => x.Id == droneId, ct) ?? throw new NotFoundException("Drone", droneId);
        if (drone.OperationalStatus != DroneOperationalStatus.Available) throw new BusinessRuleException("DRONE_UNAVAILABLE");
        var inspection = drone.TechnicalInspections.OrderByDescending(x => x.CompletedAt).FirstOrDefault(x => x.Status == DroneTechnicalInspectionStatus.Passed && x.ValidUntil > DateTime.UtcNow && x.Health is TechnicalHealth.Healthy or TechnicalHealth.Warning) ?? throw new BusinessRuleException("DRONE_TECHNICAL_INSPECTION_EXPIRED");
        if (await _db.Missions.AnyAsync(x => x.PreMissionAssessmentId == assessmentId, ct)) throw new BusinessRuleException("ASSESSMENT_ALREADY_CONSUMED");
        if (await _db.Missions.AnyAsync(x => x.UavId == droneId && x.Status != MissionStatus.Completed && x.Status != MissionStatus.Cancelled, ct)) throw new BusinessRuleException("DRONE_ALREADY_RESERVED");
        var mission = new Mission { MissionCode = $"MS-{DateTime.UtcNow:yyyyMMddHHmmssfff}", Title = title, ManagerId = _current.UserId, InspectorId = inspectorId, AssignedToUserId = inspectorId, UavId = droneId, DroneCode = drone.UavCode, RegionId = assessment.RegionId, PlannedStart = assessment.PlannedStart, PlannedEnd = assessment.PlannedEnd, ScheduledStartAt = assessment.PlannedStart, Description = "", Status = MissionStatus.Assigned, PreMissionAssessmentId = assessmentId };
        mission.MissionTargets = assessment.Assets.OrderBy(x => x.Sequence).Select(x => new MissionTarget { MissionId = mission.Id, AssetId = x.AssetId, Sequence = x.Sequence }).ToList();
        mission.Assignments.Add(new MissionAssignment { MissionId = mission.Id, UserId = inspectorId, AssignmentRole = "INSPECTOR", AssignedByUserId = _current.UserId });
        _db.Missions.Add(mission); assessment.Status = PreMissionAssessmentStatus.Consumed; assessment.ConsumedByMissionId = mission.Id; assessment.Version++;
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return mission;
    }

    private async Task RequireManager(CancellationToken ct)
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty) throw new ForbiddenException("AUTHENTICATION_REQUIRED");
        if (!_current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase) && !_current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase)) throw new ForbiddenException("ASSESSMENT_PERMISSION_REQUIRED");
        if (!await _db.Users.AnyAsync(x => x.Id == _current.UserId && (x.Status == "Active" || x.Status == "Enabled"), ct)) throw new ForbiddenException("ACTIVE_USER_REQUIRED");
    }
}
