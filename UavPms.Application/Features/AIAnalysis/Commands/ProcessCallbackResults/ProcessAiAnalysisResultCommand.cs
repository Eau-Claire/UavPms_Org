using System;
using System.Collections.Generic;
using MediatR;

namespace UavPms.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;

public class BoundingBoxDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public class DetectionDto
{
    public string CategoryCode { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public BoundingBoxDto BoundingBox { get; set; } = null!;
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
