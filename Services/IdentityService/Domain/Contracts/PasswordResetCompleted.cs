using System;

namespace UavPms.IdentityService.Domain.Contracts;

public class PasswordResetCompleted
{
    public string Email { get; set; } = string.Empty;
    public DateTime ResetAt { get; set; }
}
