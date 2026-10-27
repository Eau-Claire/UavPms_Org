using System;

namespace UavPms.AIInspectionService.Domain.Contracts;

/// <summary>
/// Event gửi tới AI worker (Python) khi một AIAnalysisRun Cloud được tạo.
/// Mỗi file (ảnh/video) publish 1 event riêng.
/// JSON shape được giữ tương thích ngược với worker: FileUrl/MediaType/MissionId lấy từ run.Media,
/// <see cref="AssetId"/> được giữ lại (nullable) và mang giá trị TowerId của media.
/// </summary>
public class AIAnalysisRequestedEvent
{
    /// <summary>Id của AIAnalysisRuns.</summary>
    public Guid RequestId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string AnalysisType { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>Người yêu cầu (run.RequestedBy); Guid.Empty khi run được tạo tự động.</summary>
    public Guid UploadedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? MediaId { get; set; }
    public Guid? MissionId { get; set; }

    /// <summary>Legacy field kept for worker compatibility; carries the media TowerId (nullable).</summary>
    public Guid? AssetId { get; set; }

    public Guid? TowerId { get; set; }
    public string? PreferredModel { get; set; }
}
