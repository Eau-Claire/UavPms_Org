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
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;
using UavPms.Shared.Contracts.Interfaces;
using UavPms.Shared.Contracts.Settings;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class DroneTechnicalInspectionService : IDroneTechnicalInspectionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _current;
    private readonly ISystemSettingService? _systemSettingService;
    private readonly IMissionRealtimeNotifier? _notifier;

    public DroneTechnicalInspectionService(
        ApplicationDbContext db, 
        ICurrentUserServices current, 
        ISystemSettingService? systemSettingService = null,
        IMissionRealtimeNotifier? notifier = null)
    {
        _db = db;
        _current = current;
        _systemSettingService = systemSettingService;
        _notifier = notifier;
    }

    public async Task<DroneTechnicalInspection> SubmitInspectionAsync(DroneInspectionSubmitRequest request, CancellationToken ct)
    {
        await RequireTechnicianOrManager(ct);

        var drone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == request.DroneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", request.DroneId);

        await EnsureCallerHasDroneAccessAsync(drone.Id, ct);

        // BR-05: Hardware UID verification and assessment drone match
        if (!string.IsNullOrWhiteSpace(request.FlightControllerUid))
        {
            var cleanUid = request.FlightControllerUid.Trim().ToUpperInvariant();
            var activeBinding = await _db.DroneFlightControllers
                .FirstOrDefaultAsync(x => x.FlightControllerUid == cleanUid && x.IsActive && !x.IsDeleted, ct);

            if (activeBinding == null)
            {
                throw new BusinessRuleException("FC_NOT_REGISTERED", $"Flight Controller {cleanUid} chưa được đăng ký với bất kỳ UAV nào.");
            }

            if (activeBinding.DroneId != drone.Id)
            {
                var pairedDrone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == activeBinding.DroneId, ct);
                var pairedCode = pairedDrone?.UavCode ?? activeBinding.DroneId.ToString();
                throw new BusinessRuleException("FC_DRONE_MISMATCH", $"Connected Flight Controller belongs to {pairedCode}, but this inspection requires {drone.UavCode}.");
            }

            activeBinding.LastSeenAt = DateTime.UtcNow;
        }

        if (request.PreMissionAssessmentId.HasValue && request.PreMissionAssessmentId.Value != Guid.Empty)
        {
            var assessment = await _db.PreMissionAssessments
                .Include(x => x.DroneCandidates)
                .SingleOrDefaultAsync(x => x.Id == request.PreMissionAssessmentId.Value && !x.IsDeleted, ct)
                ?? throw new NotFoundException("PreMissionAssessment", request.PreMissionAssessmentId.Value);

            var isCandidate = assessment.DroneCandidates.Any(x => x.DroneId == drone.Id);
            if (!isCandidate)
            {
                throw new BusinessRuleException("ASSESSMENT_DRONE_MISMATCH", $"Drone {drone.UavCode} không thuộc danh sách UAV được chỉ định trong thẩm định tiền bay {assessment.Id}.");
            }
        }

        var droneSettings = _systemSettingService != null
            ? await _systemSettingService.GetDroneThresholdSettingsAsync(ct)
            : new DroneThresholdSettingsDto();

        var metricsToEvaluate = (request.Metrics != null && request.Metrics.Count > 0)
            ? request.Metrics
            : new List<DroneMetricSubmitDto>
            {
                new("BATTERY_HEALTH", "Power", (decimal)(drone.BatteryLevel > 0 ? drone.BatteryLevel : 100), Critical: true, Passed: true),
                new("IMU_CALIBRATION", "Sensors", null, BoolValue: true, Passed: true, Critical: true),
                new("GPS_RTK_FIX", "Navigation", null, BoolValue: true, Passed: true, Critical: true),
                new("PROP_INTEGRITY", "Propulsion", null, BoolValue: true, Passed: true, Critical: true),
                new("FLIGHT_CONTROLLER", "Avionics", null, BoolValue: true, Passed: true, Critical: true),
                new("RF_LINK_TELEMETRY", "Communication", 98, Passed: true, Critical: true)
            };

        var evalResult = DroneTechnicalHealthEvaluationPolicy.Evaluate(metricsToEvaluate, drone.BatteryLevel, droneSettings);

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

        if (_notifier != null)
        {
            var isPass = evalResult.Status == DroneTechnicalInspectionStatus.Passed;
            var activeAssessments = await _db.PreMissionAssessmentDrones
                .Include(pad => pad.Assessment)
                .Where(pad => pad.DroneId == drone.Id &&
                              pad.Assessment != null &&
                              pad.Assessment.Status != PreMissionAssessmentStatus.Completed &&
                              pad.Assessment.Status != PreMissionAssessmentStatus.Cancelled &&
                              pad.Assessment.Status != PreMissionAssessmentStatus.Expired)
                .Select(pad => new { pad.AssessmentId })
                .Distinct()
                .ToListAsync(ct);

            var droneDisplayName = !string.IsNullOrWhiteSpace(drone.Model) ? $"{drone.Model} ({drone.UavCode})" : drone.UavCode;

            if (activeAssessments.Count > 0)
            {
                foreach (var a in activeAssessments)
                {
                    await _notifier.NotifyAssessmentAsync(new AssessmentRealtimeEventDto
                    {
                        AssessmentId = a.AssessmentId.ToString(),
                        Type = "DroneInspectionCompleted",
                        Status = isPass ? "PASS" : "FAIL",
                        DroneId = drone.Id.ToString(),
                        DroneCode = droneDisplayName,
                        TechnicalHealth = inspection.Health.ToString(),
                        TechnicalStatus = isPass ? "Passed" : "Failed",
                        ActorId = _current.UserId.ToString(),
                        ActorName = _current.Username ?? "Technician",
                        ActorRole = "TECHNICIAN",
                        Timestamp = DateTime.UtcNow,
                        Message = $"Kỹ thuật viên đã hoàn tất chẩn đoán thiết bị UAV {droneDisplayName}: {(isPass ? "ĐẠT" : "KHÔNG ĐẠT")}."
                    }, ct);
                }
            }
            else
            {
                await _notifier.NotifyAssessmentAsync(new AssessmentRealtimeEventDto
                {
                    AssessmentId = string.Empty,
                    Type = "DroneInspectionCompleted",
                    Status = isPass ? "PASS" : "FAIL",
                    DroneId = drone.Id.ToString(),
                    DroneCode = droneDisplayName,
                    TechnicalHealth = inspection.Health.ToString(),
                    TechnicalStatus = isPass ? "Passed" : "Failed",
                    ActorId = _current.UserId.ToString(),
                    ActorName = _current.Username ?? "Technician",
                    ActorRole = "TECHNICIAN",
                    Timestamp = DateTime.UtcNow,
                    Message = $"Kỹ thuật viên đã hoàn tất chẩn đoán thiết bị UAV {droneDisplayName}: {(isPass ? "ĐẠT" : "KHÔNG ĐẠT")}."
                }, ct);
            }
        }

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
