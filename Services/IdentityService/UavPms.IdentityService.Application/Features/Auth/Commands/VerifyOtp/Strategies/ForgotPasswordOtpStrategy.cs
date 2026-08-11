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

    public ForgotPasswordOtpStrategy(IOtpService otpService)
    {
        _otpService = otpService;
    }

    public bool CanHandle(OtpPurpose purpose)
        => purpose == OtpPurpose.ForgotPassword;

    public async Task<OtpVerifyResultDto> VerifyAsync(User user, VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString();
        var hash = TokenHasher.Hash(token);
        
        await _otpService.SaveVerificationTokenAsync(hash, user.Email, TimeSpan.FromMinutes(10));

        return new OtpVerifyResultDto
        {
            Success = true,
            Message = "Verification Successful",
            Token = token
        };
    }
}