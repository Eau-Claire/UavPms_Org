using System;

namespace UavPms.AIInspectionService.Application.Features.VisionBridge.DTOs;

public class VisionDetectionDto
{
    public string DroneId { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public DateTime Timestamp { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int TrackId { get; set; }
    public int[]? BoundingBox { get; set; }
    public string? ImageName { get; set; }
}
