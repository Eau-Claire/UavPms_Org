using System;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class MissionCommunicationLog : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid? SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = "SYSTEM"; // "MANAGER" | "INSPECTOR" | "SYSTEM"
    public string Type { get; set; } = "MESSAGE";       // "MESSAGE" | "DISPATCH" | "CONFIRM" | "POSTPONE" | "SUSPEND" | "RESUME" | "CANCEL" | "REMINDER" | "OVERDUE"
    public string Content { get; set; } = string.Empty;

    public virtual Mission? Mission { get; set; }
    public virtual User? Sender { get; set; }
}
