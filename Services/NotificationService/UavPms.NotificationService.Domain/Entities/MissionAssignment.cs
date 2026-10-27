using System;
using UavPms.NotificationService.Domain.Common;

namespace UavPms.NotificationService.Domain.Entities;

public class MissionAssignment : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid UserId { get; set; }
    public string AssignmentRole { get; set; } = string.Empty;
    public string Status { get; set; } = "Active"; // Active | Unavailable | Revoked (stored as text)
    public string ResponseStatus { get; set; } = "Pending"; // Pending | Accepted | Postponed | Replaced | Cancelled
    public bool IsRequired { get; set; } = true;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public string? ResponseReason { get; set; }
    public uint Version { get; set; } = 1;
    public Guid AssignedById { get; set; }
    public DateTime? EndedAt { get; set; }

    public virtual Mission? Mission { get; set; }
    public virtual User? User { get; set; }
}
