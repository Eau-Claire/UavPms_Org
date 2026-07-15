using System;
using System.Collections.Generic;

namespace UavPms.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;

public class MissionAiDetectionMediaDto
{
    public Guid MediaId { get; set; }
    public Guid MissionId { get; set; }
    public Guid? AssetId { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string AiSource { get; set; } = string.Empty;
    public string ValidationStatus { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int DetectionCount { get; set; }
    public List<MissionAiDetectionDto> Detections { get; set; } = new();
}

public class MissionAiDetectionDto
{
    public Guid Id { get; set; }
    public Guid MediaId { get; set; }
    public Guid? AssetId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryDescription { get; set; } = string.Empty;
    public double SeverityWeight { get; set; }
    public bool IsEmergencyClass { get; set; }
    public double ConfidenceScore { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public string AiSource { get; set; } = string.Empty;
    public Guid? AnalystId { get; set; }
    public string AnalystNotes { get; set; } = string.Empty;
    public MissionAiBoundingBoxDto? BoundingBox { get; set; }
    public string RawBoundingBox { get; set; } = string.Empty;
    public DateTime? ValidatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MissionAiBoundingBoxDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
