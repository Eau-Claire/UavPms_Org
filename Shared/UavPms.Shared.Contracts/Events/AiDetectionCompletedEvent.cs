namespace UavPms.Shared.Contracts.Events;

public sealed class AiDetectionCompletedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    /// <summary>Id of the AIAnalysisRuns row that produced the detection.</summary>
    public Guid RequestId { get; set; }
    public Guid InspectionId { get; set; }
    public Guid MissionId { get; set; }
    public Guid MediaId { get; set; }
    public Guid? TowerId { get; set; }
    public Guid AnomalyId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string DefectType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Severity { get; set; } = "Medium";
    public bool IsEmergencyClass { get; set; }
    public bool IsDefect { get; set; } = true;
    public string ImageUrl { get; set; } = string.Empty;
    public string? CropUrl { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
