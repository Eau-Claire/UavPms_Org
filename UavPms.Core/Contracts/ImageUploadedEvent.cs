using System;

namespace UavPms.Core.Contracts;

public class ImageUploadedEvent
{
    public Guid MediaId { get; set; }
    public Guid MissionId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
