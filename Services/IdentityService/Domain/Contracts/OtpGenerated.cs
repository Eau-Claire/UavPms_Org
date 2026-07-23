using System;

namespace UavPms.IdentityService.Domain.Contracts;

public class OtpGenerated
{
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiryTime { get; set; }
}
