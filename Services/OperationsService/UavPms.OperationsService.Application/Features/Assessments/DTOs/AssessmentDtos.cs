using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments.DTOs;

public record CreateAssessmentRequest(
    Guid RegionId,
    DateTime PlannedStart,
    DateTime PlannedEnd,
    IReadOnlyCollection<Guid> AssetIds,
    string? BoundaryWkt = null,
    string? IdempotencyKey = null
);

public record MissionPersonnelAssignmentRequest(
    Guid UserId,
    string Role = "INSPECTOR",
    bool IsRequired = true
);

public record CreateMissionFromAssessmentRequest(
    Guid AssessmentId,
    string Title,
    string? Description,
    List<MissionPersonnelAssignmentRequest> Personnel,
    List<Guid> DroneIds,
    string? IdempotencyKey = null,
    MissionPriority Priority = MissionPriority.Normal,
    InspectionObjective Objective = InspectionObjective.PeriodicInspection,
    IReadOnlyList<string>? PriorityDefects = null,
    string? EmergencyReason = null,
    bool IsImmediate = false
);

public record DroneMetricSubmitDto(
    string MetricCode,
    string Subsystem,
    decimal? NumericValue,
    bool? BoolValue = null,
    string? ValueText = null,
    string? Unit = null,
    bool? Passed = null,
    bool IsRequired = true,
    bool Critical = false,
    string Severity = "Medium"
);

public record DroneInspectionSubmitRequest(
    Guid DroneId,
    string? Notes,
    List<DroneMetricSubmitDto>? Metrics = null,
    string? PolicyVersion = "v2.0"
);

public record PostponeAssignmentRequest(
    string Reason
);

public record MarkAssessmentCompletedRequest(
    Guid? MissionId = null
);

public record AssessmentCheckResultDto(
    string Status,
    string? Reason,
    DateTime EvaluatedAt
);

public record PreMissionAssessmentAssetDto(
    Guid Id,
    Guid AssetId,
    int Sequence,
    string? AssetCode,
    string? AssetType,
    string? Status
);

public record PreMissionAssessmentPersonnelDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string Role,
    bool IsEligible,
    string EligibilityStatus,
    string AvailabilityStatus,
    string? ReasonCode,
    string Findings
);

public record PreMissionAssessmentDroneDto(
    Guid Id,
    Guid DroneId,
    string UavCode,
    string Model,
    bool IsEligible,
    string OperationalStatus,
    string TechnicalHealth,
    Guid? TechnicalInspectionId,
    string? ReasonCode,
    double BatteryLevel
);

public record PreMissionAssessmentDto(
    Guid Id,
    string AssessmentCode,
    Guid ManagerId,
    Guid RegionId,
    string RegionName,
    DateTime PlannedStart,
    DateTime PlannedEnd,
    string Status,
    string OverallTechnicalHealth,
    DateTime? ValidUntil,
    uint Version,
    Guid? ConsumedByMissionId,
    string Findings,
    string SiteFeasibilityStatus,
    string? EvaluationPolicyVersion,
    string? IdempotencyKey,
    IReadOnlyList<PreMissionAssessmentAssetDto> Assets,
    IReadOnlyList<PreMissionAssessmentPersonnelDto> PersonnelCandidates,
    IReadOnlyList<PreMissionAssessmentDroneDto> DroneCandidates,
    IReadOnlyList<PreMissionAssessmentDroneDto> UavCandidates,
    AssessmentCheckResultDto Site,
    AssessmentCheckResultDto Uav,
    AssessmentCheckResultDto Technical,
    AssessmentCheckResultDto Personnel,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record DroneTechnicalMetricDto(
    Guid Id,
    string MetricCode,
    string Subsystem,
    decimal? NumericValue,
    bool? BoolValue,
    string? ValueText,
    string? Unit,
    bool Passed,
    bool Critical,
    string Severity,
    bool IsRequired
);

public record DroneTechnicalInspectionDto(
    Guid Id,
    Guid DroneId,
    Guid? TechnicianUserId,
    string? TechnicianName,
    string Status,
    string Health,
    string? PolicyVersion,
    string? Notes,
    string? SourceType,
    string? SourceVersion,
    DateTime StartedAt,
    DateTime? CompletedAt,
    DateTime? ValidUntil,
    IReadOnlyList<DroneTechnicalMetricDto> Metrics
);

