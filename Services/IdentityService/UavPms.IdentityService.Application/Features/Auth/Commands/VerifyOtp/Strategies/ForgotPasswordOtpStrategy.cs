using System.Security.Claims;
using UavPms.IdentityService.Application.Common.Utilities;
using UavPms.IdentityService.Application.Features.Auth.DTOs;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Domain.Interfaces.Services;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies;

public class ForgotPasswordOtpStrategy : IOtpVerificationStrategy
{
    private readonly IOtpService _otpService;
    private readonly UavPms.Shared.Contracts.Interfaces.ISystemSettingService? _systemSettingService;

    public ForgotPasswordOtpStrategy(
        IOtpService otpService,
        UavPms.Shared.Contracts.Interfaces.ISystemSettingService? systemSettingService = null)
    {
        _otpService = otpService;
        _systemSettingService = systemSettingService;
    }

    public bool CanHandle(OtpPurpose purpose)
        => purpose == OtpPurpose.ForgotPassword;

    public async Task<OtpVerifyResultDto> VerifyAsync(User user, VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString();
        var hash = TokenHasher.Hash(token);
        
        var expiryMinutes = _systemSettingService != null
            ? (await _systemSettingService.GetTokenSettingsAsync(cancellationToken)).VerificationTokenExpiryMinutes
            : 10;

        await _otpService.SaveVerificationTokenAsync(hash, user.Email, TimeSpan.FromMinutes(expiryMinutes));

        return new OtpVerifyResultDto
        {
            Success = true,
            Message = "Verification Successful",
            Token = token
        };
    }
}