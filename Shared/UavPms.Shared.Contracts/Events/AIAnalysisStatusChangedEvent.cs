namespace UavPms.Shared.Contracts.Events;

public class AIAnalysisStatusChangedEvent
{
    public Guid UserId { get; set; }
    public Guid RequestId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? MissionId { get; set; }
    public Guid? MediaId { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int SavedDetections { get; set; }
    public int CreatedAlerts { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
