namespace UavPms.IdentityService.Infrastructure.Services.Helpers;

public static class OtpCalculations
{
    /// <summary>
    /// Expiration time for OTP in seconds.
    /// </summary>
    public static (bool IsCooldownActive, int RemainingSeconds) CalculateCooldown(
        TimeSpan? ttl,
        int totalTtlSeconds = 180,
        int cooldownSeconds = 30)
    {
        if(!ttl.HasValue) return (false, 0);
        
        // Example: TTL = 180 seconds, cooldown = 30 seconds
        var minRemainingTtl = TimeSpan.FromSeconds(totalTtlSeconds - cooldownSeconds);
        if (ttl.Value > minRemainingTtl)
        {
            var elapsedSeconds = totalTtlSeconds - (int)ttl.Value.TotalSeconds;
            var remaining = cooldownSeconds - elapsedSeconds;
            
            return (remaining > 0, remaining >0 ? remaining : 0);
        }
        
        return (false, 0);
    }
    
    /// <summary>
    /// Evaluate attempts.
    /// </summary>
    public static (bool Exceeded, int RemainingAttempts) EvaluateAttempts(
        long attempts,
        int maxAttempts = 5)
    {
        if(attempts >= maxAttempts) return (true, 0);
        
        var remaining = maxAttempts - (int)attempts;
        return (false, remaining);
    }

}