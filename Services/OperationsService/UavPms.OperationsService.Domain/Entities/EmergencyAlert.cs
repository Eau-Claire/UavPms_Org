using System;
using System.Collections.Generic;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class EmergencyAlert : BaseEntity
{
    public Guid AnomalyId { get; set; }
    public Guid TowerId { get; set; }
    public Guid? ComponentId { get; set; }
    public Guid MissionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int DeliveryLatencySeconds { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public virtual DetectedAnomaly? Anomaly { get; set; }
    public virtual Tower? Tower { get; set; }
    public virtual AssetComponent? Component { get; set; }
    public virtual Mission? Mission { get; set; }

    public virtual ICollection<AlertEscalation> AlertEscalations { get; set; } = new List<AlertEscalation>();
}
