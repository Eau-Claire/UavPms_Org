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
        await RequireTechnicianOrAdmin(ct);

        // UC-MF01-05 E4: an inspection needs real readings. There is no default "all passed" checklist any more:
        // it produced a Passed/Healthy inspection, valid for 7 days, from no data at all.
        if (request.Metrics == null || request.Metrics.Count == 0)
            throw new BusinessRuleException("METRICS_REQUIRED", "Technical inspection requires at least one diagnostic or telemetry metric.");

        var sourceType = ResolveSourceType(request);
        var conclusion = ResolveConclusion(request.Conclusion);

        var drone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == request.DroneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", request.DroneId);

        await EnsureCallerHasDroneAccessAsync(drone, ct);

        // BR-05: Hardware UID verification
        if (!string.IsNullOrWhiteSpace(request.FlightControllerUid))
        {
            var cleanUid = request.FlightControllerUid.Trim().ToUpperInvariant();
            var activeBinding = await _db.DroneFlightControllers
                .FirstOrDefaultAsync(x => x.FlightControllerUid == cleanUid && x.UnpairedAt == null && !x.IsDeleted, ct);

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

        var droneSettings = _systemSettingService != null
            ? await _systemSettingService.GetDroneThresholdSettingsAsync(ct)
            : new DroneThresholdSettingsDto();

        var evalResult = DroneTechnicalHealthEvaluationPolicy.Evaluate(request.Metrics, drone.BatteryLevel, droneSettings);

        // Technician kết luận Failed thì luôn là Failed/Critical dù số đo đẹp; kết luận Passed không được "cứu"
        // một phiếu mà số đo có lỗi nghiêm trọng, nên trạng thái tính từ số đo vẫn được giữ.
        var concludedFailed = conclusion == InspectionConclusions.Failed;
        var status = concludedFailed ? DroneTechnicalInspectionStatus.Failed : evalResult.Status;
        var health = concludedFailed ? TechnicalHealth.Critical : evalResult.Health;

        // BRL-04: thiếu hạng mục bắt buộc thì chưa đạt chuẩn (Incomplete, không Healthy) để báo đỏ và cho nhập bổ sung.
        var missingMetricCodes = request.MissingMetricCodes ?? Array.Empty<string>();
        if (missingMetricCodes.Count > 0 && status == DroneTechnicalInspectionStatus.Passed)
        {
            status = DroneTechnicalInspectionStatus.Incomplete;
            health = health == TechnicalHealth.Healthy ? TechnicalHealth.Unknown : health;
        }

        var inspection = new DroneTechnicalInspection
        {
            DroneId = drone.Id,
            TechnicianId = _current.UserId,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(droneSettings.InspectionValidityDays),
            PolicyVersion = request.PolicyVersion ?? "v2.0",
            // Không tự điền ghi chú giả: để trống khi Technician không nhập.
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            SourceType = sourceType,
            SourceVersion = "v2.0",
            Status = status,
            Health = health
        };

        var metrics = evalResult.Metrics.Select(m => ToMetricEntity(inspection, m)).ToList();

        inspection.Metrics = metrics;

        inspection.RawSnapshot = JsonSerializer.Serialize(new
        {
            totalMetrics = metrics.Count,
            criticalFailures = evalResult.AnyCriticalFailed,
            warnings = evalResult.AnyWarning,
            missingMetrics = missingMetricCodes,
            evaluatedAt = DateTime.UtcNow
        });

        _db.DroneTechnicalInspections.Add(inspection);

        // Update drone's technical health state
        drone.TechnicalHealth = inspection.Health;
        drone.LastTechnicalInspectionId = inspection.Id;
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
            var isPass = inspection.Status == DroneTechnicalInspectionStatus.Passed;
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
        var drone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == droneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", droneId);

        await EnsureCallerHasDroneAccessAsync(drone, ct);

        return await _db.DroneTechnicalInspections
            .Include(x => x.Metrics)
            .Include(x => x.Technician)
            .Where(x => x.DroneId == droneId)
            .OrderByDescending(x => x.CompletedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Không gửi sourceType thì suy ra: có UID Flight Controller là "fc", ngược lại là nhập tay.</summary>
    private static string ResolveSourceType(DroneInspectionSubmitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceType))
            return string.IsNullOrWhiteSpace(request.FlightControllerUid)
                ? InspectionSourceTypes.Manual
                : InspectionSourceTypes.FlightController;

        var sourceType = request.SourceType.Trim().ToLowerInvariant();
        if (!InspectionSourceTypes.IsValid(sourceType))
            throw new BusinessRuleException("INVALID_SOURCE_TYPE", "sourceType chỉ nhận manual, fc hoặc bist.");

        return sourceType;
    }

    /// <summary>Kết luận là tùy chọn; có thì chỉ nhận Passed hoặc Failed (không phân biệt hoa thường).</summary>
    private static string? ResolveConclusion(string? conclusion)
    {
        if (string.IsNullOrWhiteSpace(conclusion))
            return null;

        var value = conclusion.Trim();
        if (value.Equals(InspectionConclusions.Passed, StringComparison.OrdinalIgnoreCase))
            return InspectionConclusions.Passed;
        if (value.Equals(InspectionConclusions.Failed, StringComparison.OrdinalIgnoreCase))
            return InspectionConclusions.Failed;

        throw new BusinessRuleException("INVALID_CONCLUSION", "conclusion chỉ nhận Passed hoặc Failed.");
    }

    /// <summary>
    /// The CK_DroneTechnicalMetrics_SingleValue constraint allows one of NumericValue / BoolValue / ValueText.
    /// The primary reading is kept in that order and the others (for example the notes of the basic technical check)
    /// go to Metadata, so a checklist item with a reading and a note no longer fails with a 500.
    /// </summary>
    internal static DroneTechnicalMetric ToMetricEntity(DroneTechnicalInspection inspection, EvaluatedMetricResult m)
    {
        decimal? numeric = m.NumericValue;
        bool? boolValue = numeric.HasValue ? null : m.BoolValue;
        var text = string.IsNullOrWhiteSpace(m.ValueText) ? null : m.ValueText;
        string? valueText = numeric.HasValue || boolValue.HasValue ? null : text;

        var extra = new Dictionary<string, object>();
        if (text != null && valueText == null)
            extra["note"] = text;
        if (m.BoolValue.HasValue && boolValue == null)
            extra["boolValue"] = m.BoolValue.Value;
        extra["critical"] = m.Critical;

        return new DroneTechnicalMetric
        {
            Inspection = inspection,
            MetricCode = m.MetricCode,
            Subsystem = m.Subsystem,
            NumericValue = numeric,
            BoolValue = boolValue,
            ValueText = valueText,
            Unit = m.Unit,
            Passed = m.Passed,
            Severity = m.Severity,
            IsRequired = m.IsRequired,
            Metadata = JsonSerializer.Serialize(extra)
        };
    }

    /// <summary>Đọc BIST/FC: Manager, Technician, Inspector trong vùng của Drone; SystemAdmin toàn hệ thống; Analyst bị từ chối.</summary>
    private async Task EnsureCallerHasDroneAccessAsync(Uav drone, CancellationToken ct)
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        if (_current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase))
            return;

        var isAllowed = _current.Roles.Any(r =>
            r.Equals(UserRoles.Manager, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoles.Technician, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoles.Inspector, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
            throw new ForbiddenException("DRONE_ACCESS_DENIED");

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == _current.UserId, ct);
        if (user == null || (user.Status != "Active" && user.Status != "Enabled"))
            throw new ForbiddenException("ACTIVE_USER_REQUIRED");

        await DroneRegionScope.EnsureAsync(_db, _current, drone, ct);
    }

    /// <summary>Ghi BIST/FC/nhập tay: chỉ Technician và SystemAdmin (HC-02); Manager/Inspector chỉ được đọc.</summary>
    private async Task RequireTechnicianOrAdmin(CancellationToken ct)
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        var isAllowed = _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase) ||
                        _current.Roles.Contains(UserRoles.Technician, StringComparer.OrdinalIgnoreCase);

        if (!isAllowed)
            throw new ForbiddenException("TECHNICIAN_ROLE_REQUIRED");

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == _current.UserId, ct);
        if (user == null || (user.Status != "Active" && user.Status != "Enabled"))
            throw new ForbiddenException("ACTIVE_USER_REQUIRED");
    }
}
