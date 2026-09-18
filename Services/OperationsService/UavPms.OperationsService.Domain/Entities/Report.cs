using System;
using System.Collections.Generic;
using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

public class Report : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ReportType Type { get; set; } = ReportType.Defect;
    public ReportStatus Status { get; set; } = ReportStatus.Draft;
    public string? Description { get; set; }
    public Guid? TransmissionLineId { get; set; }
    public Guid? SubstationId { get; set; }
    public DateTimeOffset? DateFrom { get; set; }
    public DateTimeOffset? DateTo { get; set; }
    public int DefectCount { get; set; } = 0;
    public string? PdfFileUrl { get; set; }
    public long? PdfFileSize { get; set; }
    public string? ExcelFileUrl { get; set; }
    public long? ExcelFileSize { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    // Navigation properties
    public virtual TransmissionLine? TransmissionLine { get; set; }
    public virtual Substation? Substation { get; set; }
    public virtual User? ApprovedBy { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public virtual ICollection<ReportMission> ReportMissions { get; set; } = new List<ReportMission>();
}
