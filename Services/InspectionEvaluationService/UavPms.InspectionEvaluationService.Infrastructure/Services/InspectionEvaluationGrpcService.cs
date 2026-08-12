using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UavPms.Grpc.InspectionEvaluation;
using UavPms.InspectionEvaluationService.Application.Common.Options;
using UavPms.InspectionEvaluationService.Application.Common.Utilities;

namespace UavPms.InspectionEvaluationService.Infrastructure.Services;

public class InspectionEvaluationGrpcService : InspectionEvaluation.InspectionEvaluationBase
{
    private readonly EvaluationThresholdOptions _options;
    private readonly ILogger<InspectionEvaluationGrpcService> _logger;

    public InspectionEvaluationGrpcService(
        IOptions<EvaluationThresholdOptions> options,
        ILogger<InspectionEvaluationGrpcService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public override Task<EvaluateDetectionResponse> EvaluateDetection(
        EvaluateDetectionRequest request,
        ServerCallContext context)
    {
        var result = DetectionEvaluationEngine.Evaluate(
            request.CategoryCode,
            request.Confidence,
            request.IsEmergencyClass,
            _options);

        _logger.LogInformation(
            "Evaluated detection {DetectionId}: Category={CategoryCode}, Confidence={Confidence:P1}, Severity={Severity}, Risk={RiskLevel}, Score={PriorityScore}",
            request.DetectionId,
            request.CategoryCode,
            request.Confidence,
            result.Severity,
            result.RiskLevel,
            result.PriorityScore);

        return Task.FromResult(new EvaluateDetectionResponse
        {
            Severity = result.Severity.ToString(),
            RiskLevel = result.RiskLevel.ToString(),
            PriorityScore = result.PriorityScore,
            RequiresImmediateAlert = result.RequiresImmediateAlert,
            Reason = result.Reason
        });
    }
}