using Grpc.Core;
using Microsoft.Extensions.Logging;
using UavPms.AIInspectionService.Application.Interfaces;
using UavPms.Grpc.InspectionEvaluation;

namespace UavPms.AIInspectionService.Infrastructure.Grpc;

public class GrpcInspectionEvaluationClient(
    InspectionEvaluation.InspectionEvaluationClient client,
    ILogger<GrpcInspectionEvaluationClient> logger)
    : IInspectionEvaluationClient
{
    public async Task<DetectionEvaluationResult> EvaluateAsync(
        DetectionEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadlineCts.CancelAfter(TimeSpan.FromSeconds(3));

            var response = await client.EvaluateDetectionAsync(
                new EvaluateDetectionRequest
                {
                    CategoryCode = request.CategoryCode,
                    CategoryName = request.CategoryName,
                    Confidence = request.Confidence,
                    IsEmergencyClass = request.IsEmergencyClass,
                    MissionId = request.MissionId?.ToString() ?? string.Empty,
                    MediaId = request.MediaId.ToString(),
                    DetectionId = request.DetectionId ?? string.Empty
                },
                cancellationToken: deadlineCts.Token);

            return new DetectionEvaluationResult(
                response.Severity,
                response.RiskLevel,
                response.PriorityScore,
                response.RequiresImmediateAlert,
                response.Reason);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            logger.LogWarning(ex,
                "InspectionEvaluation gRPC service unavailable. Falling back to local critical-alert rule for Category={CategoryCode}",
                request.CategoryCode);

            return CreateFallbackResult(request);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "InspectionEvaluation gRPC call timed out. Falling back to local critical-alert rule for Category={CategoryCode}",
                request.CategoryCode);

            return CreateFallbackResult(request);
        }
    }

    private static DetectionEvaluationResult CreateFallbackResult(DetectionEvaluationRequest request)
    {
        var requiresImmediateAlert = request.IsEmergencyClass && request.Confidence >= 0.80;
        var severity = requiresImmediateAlert
            ? "Critical"
            : request.Confidence >= 0.75 ? "High" : request.Confidence >= 0.50 ? "Medium" : "Low";

        return new DetectionEvaluationResult(
            severity,
            requiresImmediateAlert ? "ImmediateAction" : "FallbackReview",
            (int)Math.Clamp(Math.Round(request.Confidence * 100), 0, 100),
            requiresImmediateAlert,
            "Fallback evaluation used because gRPC evaluation was unavailable.");
    }
}
