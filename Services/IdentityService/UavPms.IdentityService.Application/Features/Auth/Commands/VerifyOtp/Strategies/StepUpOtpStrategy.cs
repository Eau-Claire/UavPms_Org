using UavPms.IdentityService.Application.Common.Exceptions;
using UavPms.IdentityService.Application.Features.Auth.DTOs;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Domain.Interfaces.Services;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies;

public class StepUpOtpStrategy : IOtpVerificationStrategy
{
    private readonly IJwtProvider _jwtProvider;
    private readonly IOtpService _otpService;

    public StepUpOtpStrategy(IJwtProvider jwtProvider, IOtpService otpService)
    {
        _jwtProvider = jwtProvider;
        _otpService = otpService;
    }

    public bool CanHandle(OtpPurpose purpose)
        => purpose == OtpPurpose.ChangePassword ||
           purpose == OtpPurpose.ChangeEmail ||
           purpose == OtpPurpose.DeleteAccount;

    public async Task<OtpVerifyResultDto> VerifyAsync(User user, VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        if (user.Status != "Active")
        {
            throw new NotFoundException("User not found or inactive", user.Email);
        }

        var token = _jwtProvider.GenerateStepUpToken(user, request.OtpPurpose.ToString());
        await _otpService.SaveStepUpTokenAsync(
            user.Id.ToString(),
            request.OtpPurpose.ToString(),
            token,
            TimeSpan.FromMinutes(5));

        return new OtpVerifyResultDto
        {
            Success = true,
            Message = "Verification Successful",
            Token = token
        };
    }
}