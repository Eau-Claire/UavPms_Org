using System;
using System.Collections.Generic;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class AssetComponent : BaseEntity
{
    public Guid TowerId { get; set; }
    public string ComponentType { get; set; } = string.Empty;
    public string ComponentCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double CurrentHealthScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public DateTime? LastInspectedAt { get; set; }

    public virtual Tower? Tower { get; set; }
    public virtual ICollection<DetectedAnomaly> DetectedAnomalies { get; set; } = new List<DetectedAnomaly>();
    public virtual ICollection<EmergencyAlert> EmergencyAlerts { get; set; } = new List<EmergencyAlert>();
    public virtual ICollection<IncidentReport> IncidentReports { get; set; } = new List<IncidentReport>();
    public virtual ICollection<MaintenanceTicket> MaintenanceTickets { get; set; } = new List<MaintenanceTicket>();
}
