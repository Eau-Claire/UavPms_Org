using System.Collections.Generic;
using System.IO;
using MediatR;
using UavPms.AIInspectionService.Domain.Enums;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.UploadForAnalysis;

/// <summary>
/// Thông tin 1 file cần upload cho AI phân tích.
/// </summary>
public class FileUploadItem
{
    public Stream FileStream { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}

/// <summary>
/// Command upload 1 hoặc nhiều ảnh/video cho AI phân tích ad-hoc.
/// Mỗi file sẽ tạo 1 AIAnalysisRequest riêng biệt.
/// </summary>
public class UploadForAIAnalysisCommand : IRequest<List<AIAnalysisUploadResult>>
{
    public List<FileUploadItem> Files { get; set; } = new();
    public AnalysisType AnalysisType { get; set; } = AnalysisType.General;
    public string? Notes { get; set; }
}
