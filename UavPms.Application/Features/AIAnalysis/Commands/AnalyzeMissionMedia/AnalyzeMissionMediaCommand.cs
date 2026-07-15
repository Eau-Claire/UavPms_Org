using System;
using System.IO;
using MediatR;
using UavPms.Core.Enums;
using UavPms.Application.Features.AIAnalysis.Commands.UploadForAnalysis;

namespace UavPms.Application.Features.AIAnalysis.Commands.AnalyzeMissionMedia;

public class AnalyzeMissionMediaCommand : IRequest<AIAnalysisBatchUploadResult>
{
    public Guid MissionId { get; set; }
    public List<FileDataDto> Files { get; set; } = new();
    public AnalysisType AnalysisType { get; set; } = AnalysisType.General;
    public string PreferredModel { get; set; } = "SERVER";
    public string? Notes { get; set; }
}

public class FileDataDto
{
    public Stream Stream { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}

public class AIAnalysisBatchUploadResult
{
    public Guid BatchId { get; set; }
    public int TotalFiles { get; set; }
    public int AcceptedFiles { get; set; }
    public int RejectedFiles { get; set; }
    public List<Guid> RequestIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
