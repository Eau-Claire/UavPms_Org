using UavPms.IdentityService.Domain.Enums;

namespace UavPms.IdentityService.Infrastructure.Services.Helpers;

public static class RedisKeyBuilder
{
    public static string OtpKey(OtpPurpose purpose, string email)
        => $"otp:{purpose.ToString().ToLower()}: {email}";
    
    public static string AttempKey(OtpPurpose purpose, string email)
        => $"otp:{purpose.ToString().ToLower()}: {email}:attempts";
    
    public static string VerificationTokenKey(string tokenHash)
        => $"verification-token:{tokenHash}";
    
    public static string StepUpKey(string userId, string purpose)
        => $"step-up:{userId}:{purpose.ToLower()}";
}