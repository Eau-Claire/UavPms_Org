using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Infrastructure.Utilities;

namespace UavPms.IdentityService.Infrastructure.Services.Helpers;

public static class RedisKeyBuilder
{
    public static string OtpKey(OtpPurpose purpose, string email)
        => $"otp:{purpose.ToString().ToLower()}:{EmailNormalizer.Normalize(email)}";
    
    public static string AttemptsKey(OtpPurpose purpose, string email)
        => $"otp:{purpose.ToString().ToLower()}:{EmailNormalizer.Normalize(email)}:attempts";
    
    public static string VerificationTokenKey(string tokenHash)
        => $"verification-token:{tokenHash}";
    
    public static string StepUpKey(string userId, string purpose)
        => $"step-up:{userId}:{purpose.ToLower()}";
}
