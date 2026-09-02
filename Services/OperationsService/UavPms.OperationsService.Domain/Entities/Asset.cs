using System;
using System.Collections.Generic;
using UavPms.OperationsService.Domain.Common;
using NetTopologySuite.Geometries;

namespace UavPms.OperationsService.Domain.Entities;

public class Asset : BaseEntity
{
    public Guid TowerId { get; set; }
    public Guid? PowerLineId { get; set; }
    public Guid? ManagementUnitId { get; set; }
    public Point? Location { get; set; }
    public string AssetType { get; set; } = string.Empty;
    public string AssetCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double CurrentHealthScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public DateTime? LastInspectedAt { get; set; }

    public virtual Tower? Tower { get; set; }
    public virtual TransmissionLine? PowerLine { get; set; }
    public virtual ManagementUnit? ManagementUnit { get; set; }
    public virtual ICollection<MissionTarget> MissionTargets { get; set; } = new List<MissionTarget>();
    public virtual ICollection<AssetHealthHistory> HealthHistories { get; set; } = new List<AssetHealthHistory>();
    public virtual ICollection<InspectionMedia> InspectionMedias { get; set; } = new List<InspectionMedia>();
    public virtual ICollection<DetectedAnomaly> DetectedAnomalies { get; set; } = new List<DetectedAnomaly>();
    public virtual ICollection<EmergencyAlert> EmergencyAlerts { get; set; } = new List<EmergencyAlert>();
    public virtual ICollection<IncidentReport> IncidentReports { get; set; } = new List<IncidentReport>();
    public virtual ICollection<MaintenanceTicket> MaintenanceTickets { get; set; } = new List<MaintenanceTicket>();
}
