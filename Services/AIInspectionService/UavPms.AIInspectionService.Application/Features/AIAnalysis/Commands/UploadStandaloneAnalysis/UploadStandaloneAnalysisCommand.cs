using System;
using System.Collections.Generic;
using System.IO;
using MediatR;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.UploadStandaloneAnalysis;

public sealed class UploadedFileInput
{
    public Stream Stream { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
}

public sealed class UploadStandaloneAnalysisCommand : IRequest<UploadStandaloneAnalysisResult>
{
    public IReadOnlyList<UploadedFileInput> Files { get; set; } = Array.Empty<UploadedFileInput>();
    public Guid? MissionId { get; set; }
    public Guid? TowerId { get; set; }
    public string? AnalysisType { get; set; }
    public string? PreferredModel { get; set; }
    public string? Notes { get; set; }
}

public sealed class UploadedMediaRunItemDto
{
    public Guid RunId { get; set; }
    public Guid MediaId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class UploadStandaloneAnalysisResult
{
    public string Id { get; set; } = string.Empty;
    public Guid BatchId { get; set; }
    public Guid MissionId { get; set; }
    public Guid? TowerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AnalysisType { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public int FilesCount { get; set; }
    public IReadOnlyList<UploadedMediaRunItemDto> Runs { get; set; } = Array.Empty<UploadedMediaRunItemDto>();
    public IReadOnlyList<MissionAiDetectionDto> Detections { get; set; } = Array.Empty<MissionAiDetectionDto>();
}

