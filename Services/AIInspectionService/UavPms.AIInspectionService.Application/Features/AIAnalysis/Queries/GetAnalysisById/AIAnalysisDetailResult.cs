using System;
using UavPms.AIInspectionService.Domain.Enums;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetAnalysisById;

/// <summary>
/// DTO kết quả trả về khi query một AI analysis run.
/// FileUrl / MediaType / MissionId / TowerId lấy từ media của run.
/// </summary>
public class AIAnalysisDetailResult
{
    public Guid Id { get; set; }

    /// <summary>Legacy name kept for the frontend; same value as <see cref="RequestedBy"/>.</summary>
    public Guid? UploadedBy { get; set; }
    public Guid? RequestedBy { get; set; }
    public Guid MediaId { get; set; }
    public Guid? MissionId { get; set; }
    public Guid? TowerId { get; set; }
    public Guid? UavId { get; set; }
    public string ExecutionLocation { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? ModelVersion { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public AnalysisType AnalysisType { get; set; }
    public AIAnalysisStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
