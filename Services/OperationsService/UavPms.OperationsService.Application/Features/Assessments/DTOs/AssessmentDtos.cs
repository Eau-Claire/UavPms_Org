using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments.DTOs;

public record CreateAssessmentRequest(
    Guid RegionId,
    DateTime PlannedStart,
    DateTime PlannedEnd,
    IReadOnlyCollection<Guid> TowerIds,
    string? BoundaryWkt = null,
    string? IdempotencyKey = null,
    int? InspectorCount = null,
    int? AnalystCount = null,
    int? TechnicianCount = null,
    int? MaxDronesLimit = null,
    IReadOnlyCollection<Guid>? DroneIds = null
);

/// <summary>Body tùy chọn của re-evaluate: đổi số lượng nhân sự hoặc danh sách Drone đã chọn (chỉ Drone Ready).</summary>
public record ReEvaluateAssessmentRequest(
    int? InspectorCount = null,
    int? AnalystCount = null,
    int? TechnicianCount = null,
    IReadOnlyCollection<Guid>? DroneIds = null
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
    bool IsImmediate = false,
    /// <summary>BR-31: when Pending Acceptance passes this time the manager is notified. Must be in the future and
    /// not after the planned start; omitted → now + Mission:ConfirmationDeadlineHours (24 h), capped at the planned start.</summary>
    DateTime? ConfirmationDeadline = null,
    string? ManagerInstructions = null
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
    string? PolicyVersion = "v2.0",
    string? FlightControllerUid = null,
    string? SourceType = null,
    string? Conclusion = null,
    /// <summary>BRL-04: hạng mục bắt buộc mà client chưa gửi; có phần tử thì phiếu không thể Passed/Healthy.</summary>
    IReadOnlyList<string>? MissingMetricCodes = null
);

public record PostponeAssignmentRequest(
    string Reason
);

public record MarkAssessmentCompletedRequest(
    Guid? MissionId = null
);

public record CancelAssessmentRequest(
    string? Reason = null
);

public record AssessmentCheckResultDto(
    string Status,
    string? Reason,
    DateTime EvaluatedAt
);

public record PreMissionAssessmentTowerDto(
    Guid Id,
    Guid TowerId,
    int Sequence,
    string? TowerCode,
    string? PoleType
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
    double BatteryLevel,
    string? Suitability = null,
    int? EstimatedFlightsNeeded = null,
    double? CoverageDistanceKm = null,
    string? SuitabilityReason = null,
    /// <summary>Operational availability (Available, Conflict, Unavailable, Unknown) separate from technical readiness.</summary>
    bool? IsAvailable = null,
    /// <summary>Technical eligibility (Eligible, Ineligible, RequiresInspection, Unknown).</summary>
    string? TechnicalEligibilityStatus = null
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
    IReadOnlyList<PreMissionAssessmentTowerDto> Towers,
    IReadOnlyList<PreMissionAssessmentPersonnelDto> PersonnelCandidates,
    IReadOnlyList<PreMissionAssessmentDroneDto> DroneCandidates,
    IReadOnlyList<PreMissionAssessmentDroneDto> UavCandidates,
    AssessmentCheckResultDto Site,
    AssessmentCheckResultDto Uav,
    AssessmentCheckResultDto Technical,
    AssessmentCheckResultDto Personnel,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? ManagerName = null,
    int InspectorCount = 1,
    int AnalystCount = 1,
    int TechnicianCount = 1
)
{
    public Guid? CompletedMissionId => ConsumedByMissionId;
    public Guid? CompletedByMissionId => ConsumedByMissionId;
}

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
    IReadOnlyList<DroneTechnicalMetricDto> Metrics,
    string Readiness,
    IReadOnlyList<string> MissingMetrics
);

