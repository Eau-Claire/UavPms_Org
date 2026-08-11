using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MediatR;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;

public class BoundingBoxDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public class GpsDto
{
    public double? Lat { get; set; }
    public double? Lng { get; set; }
}

public class VideoMetadataDto
{
    public double? Duration { get; set; }
    public double? Fps { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

public class DetectionDto
{
    public string? Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;

    [JsonPropertyName("class")]
    public string? ClassName { get; set; }

    public double Confidence { get; set; }
    public BoundingBoxDto BoundingBox { get; set; } = null!;
    public int? FrameIndex { get; set; }
    public double? Timestamp { get; set; }
    public int? TimestampMs { get; set; }
    public string? ImageUrl { get; set; }
    public string? CropUrl { get; set; }
    public GpsDto? Gps { get; set; }
    public string? TowerId { get; set; }
    public Guid? AssetId { get; set; }
}

public class ProcessAiAnalysisResultCommand : IRequest<AiAnalysisCallbackResponseDto>
{
    public Guid RequestId { get; set; }
    public Guid? MediaId { get; set; }
    public string Status { get; set; } = string.Empty; // "Completed" or "Failed"
    public string? ModelName { get; set; }
    public string? ModelVersion { get; set; }
    public int? ProcessingTimeMs { get; set; }
    public List<DetectionDto>? Detections { get; set; }
    public VideoMetadataDto? VideoMetadata { get; set; }
    public object? RawResult { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class AiAnalysisCallbackResponseDto
{
    public Guid RequestId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SavedDetections { get; set; }
    public int CreatedAlerts { get; set; }
    public DateTime ProcessedAt { get; set; }
}
