using System;
using System.Threading.Tasks;
using UavPms.IdentityService.Domain.Enums;

namespace UavPms.IdentityService.Domain.Interfaces.Services;

public interface IOtpService
{
    Task<(bool Success, string Message)> GenerateAndSendOtpAsync(string email, OtpPurpose purpose, bool isResend = false);
    Task<(bool IsValid, string Message)> VerifyOtpAsync(string email, string code, OtpPurpose purpose);

    // Redis-backed Token Helpers
    Task SaveVerificationTokenAsync(string tokenHash, string email, TimeSpan expiry);
    Task<string?> GetVerificationTokenEmailAsync(string tokenHash);
    Task DeleteVerificationTokenAsync(string tokenHash);

    Task SaveStepUpTokenAsync(string userId, string purpose, string stepUpToken, TimeSpan expiry);
    Task<string?> GetStepUpTokenAsync(string userId, string purpose);
    Task DeleteStepUpTokenAsync(string userId, string purpose);
}
