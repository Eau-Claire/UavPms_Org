using System;
using UavPms.AIInspectionService.Domain.Enums;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetAnalysisById;

/// <summary>
/// DTO kết quả trả về khi query AI analysis request.
/// </summary>
public class AIAnalysisDetailResult
{
    public Guid Id { get; set; }
    public Guid UploadedBy { get; set; }
    public Guid? MediaId { get; set; }
    public Guid? MissionId { get; set; }
    public Guid? AssetId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public AnalysisType AnalysisType { get; set; }
    public string? Notes { get; set; }
    public AIAnalysisStatus Status { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
