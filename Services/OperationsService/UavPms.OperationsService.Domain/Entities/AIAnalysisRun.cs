using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

/// <summary>
/// Một lần chạy model AI trên một media trên Cloud.
/// </summary>
public class AIAnalysisRun : BaseEntity
{
    public const string Cloud = "Cloud";

    public Guid MediaId { get; set; }

    /// <summary>Vị trí thực thi AI ("Cloud")</summary>
    public string ExecutionLocation { get; set; } = Cloud;

    /// <summary>UAV liên kết với nhiệm vụ/lần chạy (nếu có)</summary>
    public Guid? UavId { get; set; }

    /// <summary>Người yêu cầu phân tích; NULL = tự động</summary>
    public Guid? RequestedBy { get; set; }

    public AnalysisType AnalysisType { get; set; } = AnalysisType.General;
    public string ModelName { get; set; } = "SERVER";
    public string? ModelVersion { get; set; }
    public AIAnalysisStatus Status { get; set; } = AIAnalysisStatus.Queued;
    public Guid? SourceEventId { get; set; }
    public Guid? BatchId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public virtual InspectionMedia? Media { get; set; }
    public virtual Uav? Uav { get; set; }
    public virtual User? Requester { get; set; }
    public virtual ICollection<DetectedAnomaly> DetectedAnomalies { get; set; } = new List<DetectedAnomaly>();
}
