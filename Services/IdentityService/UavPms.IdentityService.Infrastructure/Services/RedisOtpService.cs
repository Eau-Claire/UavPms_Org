using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using StackExchange.Redis;
using UavPms.IdentityService.Application.Common.Utilities;
using UavPms.IdentityService.Domain.Contracts;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Domain.Interfaces.Services;
using UavPms.IdentityService.Infrastructure.Services.Helpers;

namespace UavPms.IdentityService.Infrastructure.Services;

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

    public async Task<(bool Success, string Message)> GenerateAndSendOtpAsync(string email, OtpPurpose purpose, bool isResend = false)
    {
        var db = GetDb();
        var otpKey = RedisKeyBuilder.OtpKey(purpose, email);
        var attemptsKey = RedisKeyBuilder.AttemptsKey(purpose, email);

        if (isResend)
        {
            var ttl = await db.KeyTimeToLiveAsync(otpKey);
            var (isCooldownActive, remainingSeconds) = OtpCalculations.CalculateCooldown(ttl);
            if (isCooldownActive)
            {
                return (false, $"Please wait {remainingSeconds} seconds before requesting a new OTP.");
            }
        }

        // Tạm thời cố định mã OTP mặc định là "123456" cho author và authen (thuận tiện cho việc test)
        var code = "123456";
        var hashedCode = TokenHasher.Hash(code);
        var expiryTime = DateTime.UtcNow.AddMinutes(15);

        try
        {
            await _emailService.SendOtpEmailAsync(email, code, expiryTime);
        }
        catch (Exception)
        {
            // Bỏ qua lỗi gửi email (như thiếu SendGrid / Brevo API key) để không chặn luồng dev/test
        }

        // Save hashed OTP in Redis with 15 minutes TTL
        await db.StringSetAsync(otpKey, hashedCode, TimeSpan.FromMinutes(15));
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
        // 1. Cho phép OTP mặc định "123456" luôn hợp lệ để thuận tiện cho việc dev/test (author & authen)
        if (code == "123456")
        {
            var dbTest = GetDb();
            var testOtpKey = RedisKeyBuilder.OtpKey(purpose, email);
            var testAttemptsKey = RedisKeyBuilder.AttemptsKey(purpose, email);
            try
            {
                await dbTest.KeyDeleteAsync(new RedisKey[] { testOtpKey, testAttemptsKey });
            }
            catch
            {
                // Bỏ qua nếu Redis gặp vấn đề tạm thời
            }
            return (true, "OTP verified successfully.");
        }

        var db = GetDb();
        var otpKey = RedisKeyBuilder.OtpKey(purpose, email);
        var attemptsKey = RedisKeyBuilder.AttemptsKey(purpose, email);

        // Check if OTP key exists
        var savedOtpHash = await db.StringGetAsync(otpKey);
        if (savedOtpHash.IsNullOrEmpty)
        {
            return (false, "OTP has expired or does not exist.");
        }

        // Verify the code
        var codeHash = TokenHasher.Hash(code);
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

            var (exceeded, remainingAttempts) = OtpCalculations.EvaluateAttempts(attempts);
            if (exceeded)
            {
                // Reached 5 attempts -> delete both keys
                await db.KeyDeleteAsync(new RedisKey[] { otpKey, attemptsKey });
                return (false, "Maximum verification attempts exceeded. Please request a new OTP.");
            }

            return (false, $"Invalid OTP code. You have {remainingAttempts} attempts remaining.");
        }
    }

    // Redis-backed Token Helpers
    public async Task SaveVerificationTokenAsync(string tokenHash, string email, TimeSpan expiry)
    {
        var db = GetDb();
        var key = RedisKeyBuilder.VerificationTokenKey(tokenHash);
        await db.StringSetAsync(key, email, expiry);
    }

    public async Task<string?> GetVerificationTokenEmailAsync(string tokenHash)
    {
        var db = GetDb();
        var key = RedisKeyBuilder.VerificationTokenKey(tokenHash);
        var email = await db.StringGetAsync(key);
        return email.HasValue ? email.ToString() : null;
    }

    public async Task DeleteVerificationTokenAsync(string tokenHash)
    {
        var db = GetDb();
        var key = RedisKeyBuilder.VerificationTokenKey(tokenHash);
        await db.KeyDeleteAsync(key);
    }

    public async Task SaveStepUpTokenAsync(string userId, string purpose, string stepUpToken, TimeSpan expiry)
    {
        var db = GetDb();
        var key = RedisKeyBuilder.StepUpKey(userId, purpose);
        await db.StringSetAsync(key, stepUpToken, expiry);
    }

    public async Task<string?> GetStepUpTokenAsync(string userId, string purpose)
    {
        var db = GetDb();
        var key = RedisKeyBuilder.StepUpKey(userId, purpose);
        var token = await db.StringGetAsync(key);
        return token.HasValue ? token.ToString() : null;
    }

    public async Task DeleteStepUpTokenAsync(string userId, string purpose)
    {
        var db = GetDb();
        var key = RedisKeyBuilder.StepUpKey(userId, purpose);
        await db.KeyDeleteAsync(key);
    }
}
