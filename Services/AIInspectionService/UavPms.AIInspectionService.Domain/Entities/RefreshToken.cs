using UavPms.AIInspectionService.Domain.Common;
using System;

namespace UavPms.AIInspectionService.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? DeviceInfo { get; set; }

    public virtual User User { get; set; } = null!;
}