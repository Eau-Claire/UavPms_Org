using System;
using UavPms.Core.Enums;

namespace UavPms.Application.Features.AIAnalysis.Queries.GetAnalysisById;

/// <summary>
/// DTO kết quả trả về khi query AI analysis request.
/// </summary>
public class AIAnalysisDetailResult
{
    public Guid Id { get; set; }
    public Guid UploadedBy { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public AnalysisType AnalysisType { get; set; }
    public string? Notes { get; set; }
    public AIAnalysisStatus Status { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
