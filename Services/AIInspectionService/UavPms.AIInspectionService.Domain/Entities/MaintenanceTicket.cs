using System;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

public class MaintenanceTicket : BaseEntity
{
    public const string StatusOpen = "Open";
    public const string PriorityEmergency = "Emergency";
    public const string PriorityHigh = "High";
    public const string PriorityMedium = "Medium";
    public const string PriorityLow = "Low";

    public string TicketCode { get; set; } = string.Empty;
    public Guid AnomalyId { get; set; }
    public Guid ManagerId { get; set; }
    public Guid? TechnicianId { get; set; }
    public string Status { get; set; } = StatusOpen;
    public string Priority { get; set; } = PriorityMedium;
    public string Description { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public virtual DetectedAnomaly? Anomaly { get; set; }
    public virtual User? Manager { get; set; }
    public virtual User? Technician { get; set; }
}

