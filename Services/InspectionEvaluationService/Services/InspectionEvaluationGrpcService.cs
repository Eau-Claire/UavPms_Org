using Grpc.Core;
using UavPms.Grpc.InspectionEvaluation;

namespace UavPms.InspectionEvaluationService.Services;

public class InspectionEvaluationGrpcService(
    ILogger<InspectionEvaluationGrpcService> logger)
    : InspectionEvaluation.InspectionEvaluationBase
{
    public override Task<EvaluateDetectionResponse> EvaluateDetection(
        EvaluateDetectionRequest request,
        ServerCallContext context)
    {
        var confidence = Math.Clamp(request.Confidence, 0, 1);
        var emergencyWeight = request.IsEmergencyClass ? 45 : 0;
        var confidenceWeight = (int)Math.Round(confidence * 55);
        var priorityScore = Math.Clamp(emergencyWeight + confidenceWeight, 0, 100);

        var severity = priorityScore switch
        {
            >= 90 => "Critical",
            >= 75 => "High",
            >= 50 => "Medium",
            _ => "Low"
        };

        var riskLevel = severity switch
        {
            "Critical" => "ImmediateAction",
            "High" => "UrgentReview",
            "Medium" => "PlannedReview",
            _ => "Monitor"
        };

        var requiresImmediateAlert = request.IsEmergencyClass && confidence >= 0.80;
        var reason = requiresImmediateAlert
            ? $"Emergency category {request.CategoryCode} exceeded confidence threshold ({confidence:P1})."
            : $"Category {request.CategoryCode} evaluated with confidence {confidence:P1}.";

        logger.LogInformation(
            "Evaluated detection {DetectionId}: Category={CategoryCode}, Confidence={Confidence}, Severity={Severity}, Risk={RiskLevel}, Score={PriorityScore}",
            request.DetectionId,
            request.CategoryCode,
            confidence,
            severity,
            riskLevel,
            priorityScore);

        return Task.FromResult(new EvaluateDetectionResponse
        {
            Severity = severity,
            RiskLevel = riskLevel,
            PriorityScore = priorityScore,
            RequiresImmediateAlert = requiresImmediateAlert,
            Reason = reason
        });
    }
}
