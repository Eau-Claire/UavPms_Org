using System;

namespace UavPms.Core.Contracts;

public class OtpGenerated
{
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiryTime { get; set; }
}
