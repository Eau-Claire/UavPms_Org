using System.ComponentModel.DataAnnotations;

namespace UavPms.InspectionEvaluationService.Application.Common.Options;

public class EvaluationThresholdOptions
{
    public const string SectionName = "EvaluationThresholds";

    [Range(0, 100, ErrorMessage = "EmergencyWeight must be between 0 and 100")]
    public int EmergencyWeight { get; init; } = 45;

    [Range(0, 100, ErrorMessage = "MaxConfidenceWeight must be between 0 and 100")]
    public int MaxConfidenceWeight { get; init; } = 55;

    [Range(0.0, 1.0, ErrorMessage = "EmergencyConfidenceThreshold must be between 0.0 and 1.0")]
    public double EmergencyConfidenceThreshold { get; init; } = 0.80;

    [Range(0, 100, ErrorMessage = "CriticalScoreThreshold must be between 0 and 100")]
    public int CriticalScoreThreshold { get; init; } = 90;

    [Range(0, 100, ErrorMessage = "HighScoreThreshold must be between 0 and 100")]
    public int HighScoreThreshold { get; init; } = 75;

    [Range(0, 100, ErrorMessage = "MediumScoreThreshold must be between 0 and 100")]
    public int MediumScoreThreshold { get; init; } = 50;
}