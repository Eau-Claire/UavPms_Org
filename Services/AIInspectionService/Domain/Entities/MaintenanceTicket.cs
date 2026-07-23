using System;
using System.Collections.Generic;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

public class MaintenanceTicket : BaseEntity
{
    public string TicketCode { get; set; } = string.Empty;
    public Guid AnomalyId { get; set; }
    public Guid AssetId { get; set; }
    public Guid ManagerId { get; set; }
    public Guid TechnicianId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public virtual DetectedAnomaly? Anomaly { get; set; }
    public virtual Asset? Asset { get; set; }
    public virtual User? Manager { get; set; }
    public virtual User? Technician { get; set; }

    public virtual ICollection<MaintenanceProof> MaintenanceProofs { get; set; } = new List<MaintenanceProof>();
    public virtual ICollection<MaterialLog> MaterialLogs { get; set; } = new List<MaterialLog>();
}
