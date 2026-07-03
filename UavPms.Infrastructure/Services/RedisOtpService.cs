using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;
using UavPms.Core.Contracts;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Infrastructure.Services;

public class RedisOtpService : IOtpService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IEmailService _emailService;
    private readonly IEventPublisher _eventPublisher;

    public RedisOtpService(IConnectionMultiplexer redis, IEmailService emailService, IEventPublisher eventPublisher)
    {
        _redis = redis;
        _emailService = emailService;
        _eventPublisher = eventPublisher;
    }

    private IDatabase GetDb() => _redis.GetDatabase();

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    public async Task<(bool Success, string Message)> GenerateAndSendOtpAsync(string email, OtpPurpose purpose, bool isResend = false)
    {
        var db = GetDb();
        var otpKey = $"otp:{purpose.ToString().ToLower()}:{email}";
        var attemptsKey = $"otp:{purpose.ToString().ToLower()}:{email}:attempts";

        if (isResend)
        {
            var ttl = await db.KeyTimeToLiveAsync(otpKey);
            if (ttl.HasValue && ttl.Value > TimeSpan.FromMinutes(2.5)) // less than 30s elapsed
            {
                var elapsedSeconds = 180 - (int)ttl.Value.TotalSeconds;
                var remaining = 30 - elapsedSeconds;
                if (remaining > 0)
                {
                    return (false, $"Please wait {remaining} seconds before requesting a new OTP.");
                }
            }
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var hashedCode = HashToken(code);
        var expiryTime = DateTime.UtcNow.AddMinutes(3);

        try
        {
            await _emailService.SendOtpEmailAsync(email, code, expiryTime);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to send OTP email: {ex.Message}");
        }

        // Save hashed OTP in Redis with 3 minutes TTL
        await db.StringSetAsync(otpKey, hashedCode, TimeSpan.FromMinutes(3));
        // Reset attempts count when a new OTP is generated
        await db.KeyDeleteAsync(attemptsKey);

        await _eventPublisher.PublishAsync(new OtpGenerated
        {
            Email = email,
            ExpiryTime = expiryTime
        });

        return (true, "OTP generated and sent successfully.");
    }

    public async Task<(bool IsValid, string Message)> VerifyOtpAsync(string email, string code, OtpPurpose purpose)
    {
        var db = GetDb();
        var otpKey = $"otp:{purpose.ToString().ToLower()}:{email}";
        var attemptsKey = $"otp:{purpose.ToString().ToLower()}:{email}:attempts";

        // Check if OTP key exists
        var savedOtpHash = await db.StringGetAsync(otpKey);
        if (savedOtpHash.IsNullOrEmpty)
        {
            return (false, "OTP has expired or does not exist.");
        }

        // Verify the code
        var codeHash = HashToken(code);
        if (savedOtpHash == codeHash)
        {
            // Verify correct -> delete both keys
            await db.KeyDeleteAsync(new RedisKey[] { otpKey, attemptsKey });
            return (true, "OTP verified successfully.");
        }
        else
        {
            // Verify incorrect -> increment attempts counter
            var attempts = await db.StringIncrementAsync(attemptsKey);
            
            // Set TTL of attempts key equal to the remaining TTL of the OTP key
            var ttl = await db.KeyTimeToLiveAsync(otpKey);
            if (ttl.HasValue && ttl.Value > TimeSpan.Zero)
            {
                await db.KeyExpireAsync(attemptsKey, ttl.Value);
            }

            if (attempts >= 5)
            {
                // Reached 5 attempts -> delete both keys
                await db.KeyDeleteAsync(new RedisKey[] { otpKey, attemptsKey });
                return (false, "Maximum verification attempts exceeded. Please request a new OTP.");
            }

            var remainingAttempts = 5 - attempts;
            return (false, $"Invalid OTP code. You have {remainingAttempts} attempts remaining.");
        }
    }

    // Redis-backed Token Helpers
    public async Task SaveVerificationTokenAsync(string tokenHash, string email, TimeSpan expiry)
    {
        var db = GetDb();
        var key = $"verification-token:{tokenHash}";
        await db.StringSetAsync(key, email, expiry);
    }

    public async Task<string?> GetVerificationTokenEmailAsync(string tokenHash)
    {
        var db = GetDb();
        var key = $"verification-token:{tokenHash}";
        var email = await db.StringGetAsync(key);
        return email.HasValue ? email.ToString() : null;
    }

    public async Task DeleteVerificationTokenAsync(string tokenHash)
    {
        var db = GetDb();
        var key = $"verification-token:{tokenHash}";
        await db.KeyDeleteAsync(key);
    }

    public async Task SaveStepUpTokenAsync(string userId, string purpose, string stepUpToken, TimeSpan expiry)
    {
        var db = GetDb();
        var key = $"step-up:{userId}:{purpose.ToLower()}";
        await db.StringSetAsync(key, stepUpToken, expiry);
    }

    public async Task<string?> GetStepUpTokenAsync(string userId, string purpose)
    {
        var db = GetDb();
        var key = $"step-up:{userId}:{purpose.ToLower()}";
        var token = await db.StringGetAsync(key);
        return token.HasValue ? token.ToString() : null;
    }

    public async Task DeleteStepUpTokenAsync(string userId, string purpose)
    {
        var db = GetDb();
        var key = $"step-up:{userId}:{purpose.ToLower()}";
        await db.KeyDeleteAsync(key);
    }
}
