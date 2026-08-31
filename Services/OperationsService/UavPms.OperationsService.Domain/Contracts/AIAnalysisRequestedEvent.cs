using System;

namespace UavPms.OperationsService.Domain.Contracts;

public class AIAnalysisRequestedEvent
{
    public Guid RequestId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string AnalysisType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid UploadedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? MediaId { get; set; }
    public Guid? MissionId { get; set; }
    public Guid? ComponentId { get; set; }
    public string? PreferredModel { get; set; }
}
