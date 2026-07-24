using System;
using UavPms.NotificationService.Domain.Common;
using UavPms.NotificationService.Domain.Enums;

namespace UavPms.NotificationService.Domain.Entities;

/// <summary>
/// Yêu cầu phân tích AI ad-hoc hoặc theo mission.
/// Mỗi file ảnh hoặc video tạo một record riêng biệt.
/// </summary>
public class AIAnalysisRequest : BaseEntity
{
    /// <summary>ID batch upload nếu request được tạo từ batch mission upload</summary>
    public Guid? BatchId { get; set; }

    /// <summary>ID người upload</summary>
    public Guid UploadedBy { get; set; }

    /// <summary>ID media được phân tích nếu request gắn với InspectionMedia</summary>
    public Guid? MediaId { get; set; }

    /// <summary>ID mission nếu request được tạo trong ngữ cảnh mission</summary>
    public Guid? MissionId { get; set; }

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

    // Navigation
    public virtual User? Uploader { get; set; }
    public virtual InspectionMedia? Media { get; set; }
    public virtual Mission? Mission { get; set; }
}
