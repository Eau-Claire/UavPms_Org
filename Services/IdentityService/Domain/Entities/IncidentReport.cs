using System;
using UavPms.IdentityService.Domain.Common;

namespace UavPms.IdentityService.Domain.Entities;

public class IncidentReport : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid ReportedBy { get; set; }
    public Guid AssetId { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

    public virtual Mission? Mission { get; set; }
    public virtual User? Reporter { get; set; }
    public virtual Asset? Asset { get; set; }
}
