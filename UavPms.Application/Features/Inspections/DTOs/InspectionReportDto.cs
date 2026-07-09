using System;
using System.Collections.Generic;

namespace UavPms.Application.Features.Inspections.DTOs;

public class InspectionReportDto
{
    public Guid Id { get; set; }
    public Guid MissionId { get; set; }
    public Guid AssetId { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string AiSource { get; set; } = string.Empty;
    public string ValidationStatus { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<DetectedAnomalyDto> DetectedAnomalies { get; set; } = new();
}

public class DetectedAnomalyDto
{
    public Guid Id { get; set; }
    public Guid MediaId { get; set; }
    public Guid AssetId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string DefectType { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public string AiSource { get; set; } = string.Empty;
    public string BoundingBox { get; set; } = string.Empty;
    public DateTime? ValidatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
