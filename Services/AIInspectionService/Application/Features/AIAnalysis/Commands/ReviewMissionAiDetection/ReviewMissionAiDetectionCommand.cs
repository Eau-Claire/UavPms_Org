using System;
using MediatR;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ReviewMissionAiDetection;

public class ReviewMissionAiDetectionCommand : IRequest<MissionAiDetectionDto>
{
    public Guid MissionId { get; set; }
    public Guid DetectionId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ReviewMissionAiDetectionRequest
{
    public string Decision { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
