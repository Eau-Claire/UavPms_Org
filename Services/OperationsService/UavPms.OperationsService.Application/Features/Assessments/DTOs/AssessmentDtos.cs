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
    string? IdempotencyKey = null
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
    List<DroneMetricSubmitDto> Metrics,
    string? PolicyVersion = "v2.0"
);

public record PostponeAssignmentRequest(
    string Reason
);
