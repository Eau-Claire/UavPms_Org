using System;
using System.Collections.Generic;
using UavPms.NotificationService.Domain.Common;

namespace UavPms.NotificationService.Domain.Entities;

public class EmergencyAlert : BaseEntity
{
    public Guid AnomalyId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid MissionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int DeliveryLatencySeconds { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public virtual DetectedAnomaly? Anomaly { get; set; }
    public virtual Asset? Asset { get; set; }
    public virtual Mission? Mission { get; set; }

    public virtual ICollection<AlertEscalation> AlertEscalations { get; set; } = new List<AlertEscalation>();
}
