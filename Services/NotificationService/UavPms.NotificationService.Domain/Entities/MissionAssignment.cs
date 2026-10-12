using System;
using UavPms.NotificationService.Domain.Common;

namespace UavPms.NotificationService.Domain.Entities;

public class MissionAssignment : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid UserId { get; set; }
    public string AssignmentRole { get; set; } = string.Empty;
    public int Status { get; set; } = 1; // 1 = Active, 2 = Revoked
    public int ResponseStatus { get; set; } = 0; // 0 = Pending, 1 = Accepted, 2 = Rejected, 3 = Postponed
    public bool IsRequired { get; set; } = true;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public string? ResponseReason { get; set; }
    public uint Version { get; set; } = 1;
    public Guid AssignedByUserId { get; set; }
    public DateTime? EndedAt { get; set; }

    public virtual Mission? Mission { get; set; }
    public virtual User? User { get; set; }
}
