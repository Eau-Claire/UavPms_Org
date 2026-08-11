using System;
using UavPms.InspectionEvaluationService.Application.Common.Options;
using UavPms.InspectionEvaluationService.Domain.Enums;

namespace UavPms.InspectionEvaluationService.Application.Common.Utilities;

public static class DetectionEvaluationEngine
{
    public record EvaluationResult(
        EvaluationSeverity Severity,
        EvaluationRiskLevel RiskLevel,
        int PriorityScore,
        bool RequiresImmediateAlert,
        string Reason
    );

    public static EvaluationResult Evaluate(
        string categoryCode,
        double confidence,
        bool isEmergencyClass,
        EvaluationThresholdOptions options)
    {
        var clampedConfidence = Math.Clamp(confidence, 0.0, 1.0);
        var emergencyWeight = isEmergencyClass ? options.EmergencyWeight : 0;
        var confidenceWeight = (int)Math.Round(clampedConfidence * options.MaxConfidenceWeight);
        var priorityScore = Math.Clamp(emergencyWeight + confidenceWeight, 0, 100);

        var severity = priorityScore switch
        {
            var s when s >= options.CriticalScoreThreshold => EvaluationSeverity.Critical,
            var s when s >= options.HighScoreThreshold => EvaluationSeverity.High,
            var s when s >= options.MediumScoreThreshold => EvaluationSeverity.Medium,
            _ => EvaluationSeverity.Low
        };

        var riskLevel = severity switch
        {
            EvaluationSeverity.Critical => EvaluationRiskLevel.ImmediateAction,
            EvaluationSeverity.High => EvaluationRiskLevel.UrgentReview,
            EvaluationSeverity.Medium => EvaluationRiskLevel.PlannedReview,
            _ => EvaluationRiskLevel.Monitor
        };

        var requiresImmediateAlert = isEmergencyClass && clampedConfidence >= options.EmergencyConfidenceThreshold;
        var reason = requiresImmediateAlert
            ? $"Emergency category {categoryCode} exceeded confidence threshold ({clampedConfidence:P1})."
            : $"Category {categoryCode} evaluated with confidence {clampedConfidence:P1}.";

        return new EvaluationResult(severity, riskLevel, priorityScore, requiresImmediateAlert, reason);
    }
}
