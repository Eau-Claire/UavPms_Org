using System;
using MediatR;
using UavPms.Core.Enums;
using UavPms.Application.Features.AIAnalysis.Commands.UploadForAnalysis;

namespace UavPms.Application.Features.AIAnalysis.Commands.AnalyzeExistingMedia;

public class AnalyzeExistingMediaCommand : IRequest<AIAnalysisUploadResult>
{
    public Guid MissionId { get; set; }
    public Guid MediaId { get; set; }
    public AnalysisType AnalysisType { get; set; } = AnalysisType.General;
    public string PreferredModel { get; set; } = "SERVER";
    public string? Notes { get; set; }
}
