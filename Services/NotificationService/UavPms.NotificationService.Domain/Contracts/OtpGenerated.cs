using System;

namespace UavPms.NotificationService.Domain.Contracts;

public class OtpGenerated
{
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiryTime { get; set; }
}
