using System;
using System.Collections.Generic;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

/// <summary>
/// Cảnh báo khẩn cấp. Nhiệm vụ và cột điện suy ra qua Anomaly → Media → Mission và Anomaly.TowerId.
/// </summary>
public class EmergencyAlert : BaseEntity
{
    public Guid AnomalyId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? Notes { get; set; }

    public virtual DetectedAnomaly? Anomaly { get; set; }
    public virtual User? ReviewedBy { get; set; }
    public virtual ICollection<AlertEscalation> AlertEscalations { get; set; } = new List<AlertEscalation>();
}
