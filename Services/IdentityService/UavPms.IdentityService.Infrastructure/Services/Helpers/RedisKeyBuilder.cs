using UavPms.IdentityService.Domain.Enums;

namespace UavPms.IdentityService.Infrastructure.Services.Helpers;

public static class RedisKeyBuilder
{
    public static string OtpKey(OtpPurpose purpose, string email)
        => $"otp:{purpose.ToString().ToLower()}:{NormalizeEmail(email)}";
    
    public static string AttemptsKey(OtpPurpose purpose, string email)
        => $"otp:{purpose.ToString().ToLower()}:{NormalizeEmail(email)}:attempts";
    
    public static string VerificationTokenKey(string tokenHash)
        => $"verification-token:{tokenHash}";
    
    public static string StepUpKey(string userId, string purpose)
        => $"step-up:{userId}:{purpose.ToLower()}";

    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();
}
