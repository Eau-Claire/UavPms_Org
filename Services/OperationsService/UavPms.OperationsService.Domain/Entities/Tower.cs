using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class Tower : BaseEntity
{
    public Guid? RegionId { get; set; }
    public Guid LineAssetId { get; set; }
    public string TowerCode { get; set; } = string.Empty;
    public string LineCode { get; set; } = string.Empty;
    public string Voltage { get; set; } = string.Empty;
    public string? PoleType { get; set; }
    public string? Address { get; set; }
    public Geometry? Geom { get; set; }
    public string RiskLevel { get; set; } = "Normal";
    public int ActiveDefectsCount { get; set; } = 0;
    public DateTime? LastInspectedAt { get; set; }

    public virtual Region? Region { get; set; }
    public virtual TransmissionLine? TransmissionLine { get; set; }
    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public virtual ICollection<TowerConditionHistory> ConditionHistories { get; set; } = new List<TowerConditionHistory>();
    public virtual ICollection<InspectionMedia> InspectionMedia { get; set; } = new List<InspectionMedia>();
    public virtual ICollection<MaintenanceTicket> MaintenanceTickets { get; set; } = new List<MaintenanceTicket>();
    public virtual ICollection<EmergencyAlert> EmergencyAlerts { get; set; } = new List<EmergencyAlert>();
    public virtual ICollection<IncidentReport> IncidentReports { get; set; } = new List<IncidentReport>();
}
