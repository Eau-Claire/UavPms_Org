using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

/// <summary>
/// Một lần chạy model AI trên một media, ở thiết bị edge hoặc trên cloud.
/// Nhánh edge ghi run Completed ngay khi nhận kết quả; nhánh cloud tạo run Pending rồi cập nhật khi có callback.
/// </summary>
public class AIAnalysisRun : BaseEntity
{
    public const string Edge = "Edge";
    public const string Cloud = "Cloud";

    public Guid MediaId { get; set; }

    /// <summary>"Edge" hoặc "Cloud"</summary>
    public string ExecutionLocation { get; set; } = Cloud;

    /// <summary>Thiết bị edge đã chạy model (chỉ nhánh Edge)</summary>
    public Guid? UavId { get; set; }

    /// <summary>Người yêu cầu phân tích (chỉ nhánh Cloud); NULL = tự động</summary>
    public Guid? RequestedBy { get; set; }

    public AnalysisType AnalysisType { get; set; } = AnalysisType.General;
    public string ModelName { get; set; } = "SERVER";
    public string? ModelVersion { get; set; }
    public AIAnalysisStatus Status { get; set; } = AIAnalysisStatus.Pending;
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
