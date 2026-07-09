using System;
using UavPms.Core.Enums;

namespace UavPms.Application.Features.AIAnalysis.Commands.UploadForAnalysis;

/// <summary>
/// Kết quả trả về cho 1 file sau khi upload phân tích AI thành công.
/// </summary>
public class AIAnalysisUploadResult
{
    public Guid Id { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public AnalysisType AnalysisType { get; set; }
    public AIAnalysisStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
