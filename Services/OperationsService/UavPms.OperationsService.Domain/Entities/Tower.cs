using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class Tower : BaseEntity
{
    public Guid LineAssetId { get; set; }
    public string TowerCode { get; set; } = string.Empty;
    public Point? Geom { get; set; }
    public double CurrentHealthScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public DateTime? LastInspectedAt { get; set; }

    public virtual TransmissionLine? TransmissionLine { get; set; }
    public virtual ICollection<AssetComponent> AssetComponents { get; set; } = new List<AssetComponent>();
    public virtual ICollection<MissionTarget> MissionTargets { get; set; } = new List<MissionTarget>();
    public virtual ICollection<InspectionMedia> InspectionMedias { get; set; } = new List<InspectionMedia>();
    public virtual ICollection<DetectedAnomaly> DetectedAnomalies { get; set; } = new List<DetectedAnomaly>();
    public virtual ICollection<TowerHealthHistory> HealthHistories { get; set; } = new List<TowerHealthHistory>();
    public virtual ICollection<MaintenanceTicket> MaintenanceTickets { get; set; } = new List<MaintenanceTicket>();
    public virtual ICollection<EmergencyAlert> EmergencyAlerts { get; set; } = new List<EmergencyAlert>();
}
