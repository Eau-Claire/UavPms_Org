using System;

namespace UavPms.AIInspectionService.Domain.Contracts;

public class PasswordResetRequested
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiryTime { get; set; }
}
