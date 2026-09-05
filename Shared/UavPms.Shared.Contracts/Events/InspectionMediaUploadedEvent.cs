namespace UavPms.Shared.Contracts.Events;

/// <summary>
/// Integration event emitted after OperationsService has validated and persisted
/// authoritative mission inspection media.
/// </summary>
public sealed class InspectionMediaUploadedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid MediaId { get; set; }
    public Guid MissionId { get; set; }
    public Guid AssetId { get; set; }
    public Guid UploadedBy { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string AnalysisType { get; set; } = "General";
    public string PreferredModel { get; set; } = "SERVER";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
