using System;

namespace UavPms.OperationsService.Domain.Entities;

public class UserRole
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public virtual User? User { get; set; }
    public virtual Role? Role { get; set; }
}