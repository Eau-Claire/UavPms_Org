using System;
using UavPms.Core.Common;
using UavPms.Core.Enums;

namespace UavPms.Core.Entities;

/// <summary>
/// Yêu cầu phân tích AI ad-hoc — không liên kết với mission cụ thể.
/// Hỗ trợ: exploratory analysis, model validation, incident investigation.
/// Mỗi file (ảnh hoặc video) tạo 1 record riêng biệt.
/// </summary>
public class AIAnalysisRequest : BaseEntity
{
    /// <summary>ID người upload</summary>
    public Guid UploadedBy { get; set; }

    /// <summary>URL file đã lưu trên object storage</summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>Loại media: Image hoặc Video</summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>Loại phân tích AI</summary>
    public AnalysisType AnalysisType { get; set; } = AnalysisType.General;

    /// <summary>Ghi chú từ người upload</summary>
    public string? Notes { get; set; }

    /// <summary>Trạng thái xử lý</summary>
    public AIAnalysisStatus Status { get; set; } = AIAnalysisStatus.Pending;

    /// <summary>Kết quả phân tích AI (JSON)</summary>
    public string? Result { get; set; }

    /// <summary>Thời điểm hoàn thành phân tích</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>ID của batch upload (cho phép gom nhóm các phân tích cùng đợt)</summary>
    public Guid? BatchId { get; set; }

    // Navigation
    public virtual User? Uploader { get; set; }
}
