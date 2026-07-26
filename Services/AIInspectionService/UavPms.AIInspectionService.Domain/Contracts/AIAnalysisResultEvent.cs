using System;
using System.Collections.Generic;

namespace UavPms.AIInspectionService.Domain.Contracts;

public class AIAnalysisResultEvent
{
    public Guid EventId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid AnalysisId { get; set; }
    public Guid InspectionId { get; set; }
    public Guid? MediaId { get; set; }
    public Guid? MissionId { get; set; }
    public Guid? AssetId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public string? ModelVersion { get; set; }
    public int? ProcessingTimeMs { get; set; }
    public List<AIAnalysisResultDetectionEvent> Results { get; set; } = [];
    public AIAnalysisResultVideoMetadataEvent? VideoMetadata { get; set; }
    public object? RawResult { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ProcessedAt { get; set; }
}

public class AIAnalysisResultDetectionEvent
{
    public string? Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string? Class { get; set; }
    public double Confidence { get; set; }
    public AIAnalysisResultBoundingBoxEvent BoundingBox { get; set; } = new();
    public int? FrameIndex { get; set; }
    public double? Timestamp { get; set; }
    public int? TimestampMs { get; set; }
    public string? ImageUrl { get; set; }
    public string? CropUrl { get; set; }
    public AIAnalysisResultGpsEvent? Gps { get; set; }
    public string? TowerId { get; set; }
    public Guid? AssetId { get; set; }
}

public class AIAnalysisResultBoundingBoxEvent
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public class AIAnalysisResultGpsEvent
{
    public double? Lat { get; set; }
    public double? Lng { get; set; }
}

public class AIAnalysisResultVideoMetadataEvent
{
    public double? Duration { get; set; }
    public double? Fps { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}
