using System;
using System.Collections.Generic;
using UavPms.AIInspectionService.Domain.Common;
using UavPms.AIInspectionService.Domain.Enums;

namespace UavPms.AIInspectionService.Domain.Entities;

/// <summary>
/// Một lần chạy model AI trên một media (bảng AIAnalysisRuns), ở thiết bị edge hoặc trên cloud.
/// Nhánh Edge ghi run Completed ngay khi nhận kết quả; nhánh Cloud tạo run Pending rồi cập nhật theo callback.
/// File URL / media type / mission lấy qua <see cref="Media"/>.
/// </summary>
public class AIAnalysisRun : BaseEntity
{
    public const string Edge = "Edge";
    public const string Cloud = "Cloud";

    /// <summary>Media được phân tích (bắt buộc)</summary>
    public Guid MediaId { get; set; }

    /// <summary>"Edge" hoặc "Cloud"</summary>
    public string ExecutionLocation { get; set; } = Cloud;

    /// <summary>Thiết bị edge đã chạy model (chỉ nhánh Edge)</summary>
    public Guid? UavId { get; set; }

    /// <summary>Người yêu cầu phân tích (chỉ nhánh Cloud); NULL = tự động</summary>
    public Guid? RequestedBy { get; set; }

    /// <summary>Loại phân tích AI (lưu dạng chuỗi)</summary>
    public AnalysisType AnalysisType { get; set; } = AnalysisType.General;

    /// <summary>Runtime/model được chọn. Dùng cùng media và analysis type cho idempotency.</summary>
    public string ModelName { get; set; } = "SERVER";

    public string? ModelVersion { get; set; }

    /// <summary>Trạng thái xử lý (lưu dạng chuỗi)</summary>
    public AIAnalysisStatus Status { get; set; } = AIAnalysisStatus.Pending;

    /// <summary>Integration event đã tạo ra run (idempotent consumer)</summary>
    public Guid? SourceEventId { get; set; }

    /// <summary>ID batch upload nếu run được tạo từ batch mission upload</summary>
    public Guid? BatchId { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    // Navigation
    public virtual InspectionMedia? Media { get; set; }
    public virtual Uav? Uav { get; set; }
    public virtual User? Requester { get; set; }
    public virtual ICollection<DetectedAnomaly> DetectedAnomalies { get; set; } = new List<DetectedAnomaly>();
}
