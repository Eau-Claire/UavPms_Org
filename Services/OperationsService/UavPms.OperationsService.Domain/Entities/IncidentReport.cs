using System;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class IncidentReport : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid ReportedBy { get; set; }

    /// <summary>NULL = sự cố không xảy ra tại cột điện nào</summary>
    public Guid? TowerId { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

    public virtual Mission? Mission { get; set; }
    public virtual User? Reporter { get; set; }
    public virtual Tower? Tower { get; set; }
}
