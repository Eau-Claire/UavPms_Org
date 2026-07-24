using System;
using MediatR;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.UploadForAnalysis;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.AnalyzeExistingMedia;

public class AnalyzeExistingMediaCommand : IRequest<AIAnalysisUploadResult>
{
    public Guid MissionId { get; set; }
    public Guid MediaId { get; set; }
    public AnalysisType AnalysisType { get; set; } = AnalysisType.General;
    public string PreferredModel { get; set; } = "SERVER";
    public string? Notes { get; set; }
}
