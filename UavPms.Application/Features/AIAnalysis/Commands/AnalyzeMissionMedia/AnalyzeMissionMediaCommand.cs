using System;
using System.IO;
using MediatR;
using UavPms.Core.Enums;
using UavPms.Application.Features.AIAnalysis.Commands.UploadForAnalysis;

namespace UavPms.Application.Features.AIAnalysis.Commands.AnalyzeMissionMedia;

public class AnalyzeMissionMediaCommand : IRequest<AIAnalysisUploadResult>
{
    public Guid MissionId { get; set; }
    public Guid AssetId { get; set; }
    public Stream FileStream { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public AnalysisType AnalysisType { get; set; } = AnalysisType.General;
    public string PreferredModel { get; set; } = "SERVER";
    public string? Notes { get; set; }
}
