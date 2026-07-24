namespace UavPms.AIInspectionService.Application.Interfaces;

public record DetectionEvaluationResult(
    string Severity,
    string RiskLevel,
    int PriorityScore,
    bool RequiresImmediateAlert,
    string Reason);

public record DetectionEvaluationRequest(
    string CategoryCode,
    string CategoryName,
    double Confidence,
    bool IsEmergencyClass,
    Guid? MissionId,
    Guid MediaId,
    string? DetectionId);

public interface IInspectionEvaluationClient
{
    Task<DetectionEvaluationResult> EvaluateAsync(
        DetectionEvaluationRequest request,
        CancellationToken cancellationToken);
}
