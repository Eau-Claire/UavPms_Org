namespace UavPms.Shared.Contracts.Events;

public class DefectDetectedEvent
{
    public Guid InspectionId { get; set; }
    public Guid RecordId { get; set; }
    public Guid MissionId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsDefect { get; set; }
    public string DefectType { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
}
