using System;
using System.Collections.Generic;
using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

/// <summary>
/// Báo cáo không lưu tệp: nội dung luôn sinh trực tiếp từ phạm vi DateFrom/DateTo/RegionId/ReportMissions.
/// </summary>
public class Report : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ReportType Type { get; set; } = ReportType.Defect;
    public ReportStatus Status { get; set; } = ReportStatus.Draft;
    public string? Description { get; set; }
    public Guid? RegionId { get; set; }
    public DateTimeOffset? DateFrom { get; set; }
    public DateTimeOffset? DateTo { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>
    /// Bản chụp JSONB đông cứng số liệu báo cáo lúc approved_at để bảo đảm tính toàn vẹn kiểm toán.
    /// </summary>
    public string? Snapshot { get; set; }

    public virtual Region? Region { get; set; }
    public virtual User? ApprovedBy { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public virtual ICollection<ReportMission> ReportMissions { get; set; } = new List<ReportMission>();
}
