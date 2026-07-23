using System;

namespace UavPms.AIInspectionService.Domain.Contracts;

/// <summary>
/// Event published khi người dùng yêu cầu phân tích AI ad-hoc.
/// Consumer sẽ gọi AI service và cập nhật kết quả.
/// Mỗi file (ảnh/video) publish 1 event riêng.
/// </summary>
public class AIAnalysisRequestedEvent
{
    public Guid RequestId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string AnalysisType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid UploadedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? MediaId { get; set; }
    public Guid? MissionId { get; set; }
    public Guid? AssetId { get; set; }
    public string? PreferredModel { get; set; }
}
