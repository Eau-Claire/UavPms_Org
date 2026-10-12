using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.OperationsService.Application.Features.Assessments.Policies;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Interfaces;
using UavPms.Shared.Contracts.Settings;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class DroneTechnicalInspectionService : IDroneTechnicalInspectionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _current;
    private readonly ISystemSettingService? _systemSettingService;

    public DroneTechnicalInspectionService(
        ApplicationDbContext db, 
        ICurrentUserServices current, 
        ISystemSettingService? systemSettingService = null)
    {
        _db = db;
        _current = current;
        _systemSettingService = systemSettingService;
    }

    public async Task<DroneTechnicalInspection> SubmitInspectionAsync(DroneInspectionSubmitRequest request, CancellationToken ct)
    {
        await RequireTechnicianOrManager(ct);

        var drone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == request.DroneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", request.DroneId);

        await EnsureCallerHasDroneAccessAsync(drone.Id, ct);

        var droneSettings = _systemSettingService != null
            ? await _systemSettingService.GetDroneThresholdSettingsAsync(ct)
            : new DroneThresholdSettingsDto();

        var evalResult = DroneTechnicalHealthEvaluationPolicy.Evaluate(request.Metrics, drone.BatteryLevel, droneSettings);

        var inspection = new DroneTechnicalInspection
        {
            DroneId = drone.Id,
            TechnicianUserId = _current.UserId,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(droneSettings.InspectionValidityDays),
            PolicyVersion = request.PolicyVersion ?? "v2.0",
            Notes = request.Notes ?? "Kiểm định kỹ thuật tự động trước khi bay (BIST/Telemetry)",
            SourceType = "bist",
            SourceVersion = "v2.0",
            Status = evalResult.Status,
            Health = evalResult.Health
        };

        var metrics = evalResult.Metrics.Select(m => new DroneTechnicalMetric
        {
            Inspection = inspection,
            MetricCode = m.MetricCode,
            Subsystem = m.Subsystem,
            NumericValue = m.NumericValue,
            BoolValue = m.BoolValue,
            ValueText = m.ValueText,
            Unit = m.Unit,
            Passed = m.Passed,
            Critical = m.Critical,
            Severity = m.Severity,
            IsRequired = m.IsRequired
        }).ToList();

        inspection.Metrics = metrics;

        inspection.RawSnapshot = JsonSerializer.Serialize(new
        {
            totalMetrics = metrics.Count,
            criticalFailures = evalResult.AnyCriticalFailed,
            warnings = evalResult.AnyWarning,
            evaluatedAt = DateTime.UtcNow
        });

        _db.DroneTechnicalInspections.Add(inspection);

        // Update drone's technical health state
        drone.TechnicalHealth = inspection.Health;
        drone.LastTechnicalInspectionId = inspection.Id;
        drone.TechnicalHealthUpdatedAt = DateTime.UtcNow;
        drone.Version++;

        // Audit log
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _current.UserId,
            TableName = "DroneTechnicalInspections",
            RecordId = inspection.Id,
            ActionType = "INSPECTION_SUBMITTED",
            OldValues = "{}",
            NewValues = JsonSerializer.Serialize(new
            {
                droneId = drone.Id,
                status = inspection.Status.ToString(),
                health = inspection.Health.ToString()
            }),
            IpAddress = _current.IpAddress ?? "",
            UserAgent = _current.UserAgent ?? ""
        });

        await _db.SaveChangesAsync(ct);
        return inspection;
    }

    public async Task<DroneTechnicalInspection?> GetLatestInspectionAsync(Guid droneId, CancellationToken ct)
    {
        await EnsureCallerHasDroneAccessAsync(droneId, ct);

        return await _db.DroneTechnicalInspections
            .Include(x => x.Metrics)
            .Include(x => x.Technician)
            .Where(x => x.DroneId == droneId)
            .OrderByDescending(x => x.CompletedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task EnsureCallerHasDroneAccessAsync(Guid droneId, CancellationToken ct)
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        if (!await _db.Uavs.AnyAsync(x => x.Id == droneId && !x.IsDeleted, ct))
            throw new NotFoundException("Drone", droneId);

        if (_current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase))
            return;

        var isAllowed = _current.Roles.Any(r =>
            r.Equals(UserRoles.Manager, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoles.MaintenanceTechnician, StringComparison.OrdinalIgnoreCase) ||
            r.Equals("Technician", StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoles.Inspector, StringComparison.OrdinalIgnoreCase) ||
            r.Equals("Pilot", StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
            throw new ForbiddenException("DRONE_ACCESS_DENIED");

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == _current.UserId, ct);
        if (user == null || (user.Status != "Active" && user.Status != "Enabled"))
            throw new ForbiddenException("ACTIVE_USER_REQUIRED");

        var userScopes = await _db.UserGeographicScopes
            .Where(s => s.UserId == _current.UserId)
            .Select(s => s.RegionId)
            .ToListAsync(ct);

        if (userScopes.Count > 0 && !userScopes.Contains(null))
        {
            var droneMissionRegions = await _db.Missions
                .Where(m => m.UavId == droneId)
                .Select(m => (Guid?)m.RegionId)
                .Distinct()
                .ToListAsync(ct);

            var droneAssessmentRegions = await _db.PreMissionAssessmentDrones
                .Where(d => d.DroneId == droneId && d.Assessment != null)
                .Select(d => (Guid?)d.Assessment!.RegionId)
                .Distinct()
                .ToListAsync(ct);

            var associatedRegions = droneMissionRegions.Concat(droneAssessmentRegions).Distinct().ToList();

            if (associatedRegions.Count > 0 && !associatedRegions.Any(r => userScopes.Contains(r)))
            {
                throw new ForbiddenException("DRONE_ACCESS_DENIED");
            }
        }
    }

    private async Task RequireTechnicianOrManager(CancellationToken ct)
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        var isAllowed = _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase) ||
                        _current.Roles.Contains(UserRoles.Manager, StringComparer.OrdinalIgnoreCase) ||
                        _current.Roles.Contains(UserRoles.MaintenanceTechnician, StringComparer.OrdinalIgnoreCase) ||
                        _current.Roles.Contains("Technician", StringComparer.OrdinalIgnoreCase);

        if (!isAllowed)
            throw new ForbiddenException("TECHNICIAN_OR_MANAGER_ROLE_REQUIRED");

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == _current.UserId, ct);
        if (user == null || (user.Status != "Active" && user.Status != "Enabled"))
            throw new ForbiddenException("ACTIVE_USER_REQUIRED");
    }
}
