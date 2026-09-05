using System;
using MediatR;
using UavPms.AIInspectionService.Domain.Enums;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.AnalyzeExistingMedia;

public class AnalyzeExistingMediaCommand : IRequest<AIAnalysisReanalysisResult>
{
    public Guid MissionId { get; set; }
    public Guid MediaId { get; set; }
    public AnalysisType AnalysisType { get; set; } = AnalysisType.General;
    public string PreferredModel { get; set; } = "SERVER";
    public string? Notes { get; set; }
}

public sealed class AIAnalysisReanalysisResult
{
    public Guid Id { get; set; }
    public Guid MediaId { get; set; }
    public Guid MissionId { get; set; }
    public Guid AssetId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public AnalysisType AnalysisType { get; set; }
    public AIAnalysisStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
