using System;

namespace UavPms.OperationsService.Domain.Contracts;

public class PasswordResetCompleted
{
    public string Email { get; set; } = string.Empty;
    public DateTime ResetAt { get; set; }
}
