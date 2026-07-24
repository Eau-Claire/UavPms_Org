using System;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

public class MaterialLog : BaseEntity
{
    public Guid TicketId { get; set; }
    public Guid LoggedBy { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string ComponentCode { get; set; } = string.Empty;
    public int QuantityUsed { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string FieldObservations { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    public virtual MaintenanceTicket? Ticket { get; set; }
    public virtual User? Logger { get; set; }
}
