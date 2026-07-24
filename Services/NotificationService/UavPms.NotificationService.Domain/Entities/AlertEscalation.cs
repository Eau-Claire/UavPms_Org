using System;
using UavPms.NotificationService.Domain.Common;

namespace UavPms.NotificationService.Domain.Entities;

public class AlertEscalation : BaseEntity
{
    public Guid AlertId { get; set; }
    public Guid EscalatedBy { get; set; }
    public Guid EscalatedTo { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime EscalatedAt { get; set; } = DateTime.UtcNow;

    public virtual EmergencyAlert? Alert { get; set; }
    public virtual User? EscalatedByUser { get; set; }
    public virtual User? EscalatedToUser { get; set; }
}
