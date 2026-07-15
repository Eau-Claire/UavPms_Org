using System;

namespace UavPms.Application.Features.VisionBridge.DTOs;

public class VisionDetectionResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? RecordId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
