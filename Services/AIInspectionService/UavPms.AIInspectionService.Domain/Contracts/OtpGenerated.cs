using System;

namespace UavPms.AIInspectionService.Domain.Contracts;

public class OtpGenerated
{
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiryTime { get; set; }
}
