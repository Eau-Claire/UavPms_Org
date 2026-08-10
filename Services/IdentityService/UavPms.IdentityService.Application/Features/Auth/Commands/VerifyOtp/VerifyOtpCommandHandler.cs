using MediatR;
using UavPms.IdentityService.Application.Common.Exceptions;
using UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies;
using UavPms.IdentityService.Application.Features.Auth.DTOs;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Domain.Interfaces.Services;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, OtpVerifyResultDto>
{
    private readonly IOtpService _otpService;
    private readonly IUserRepository _userRepository;
    private readonly OtpVerificationStrategyResolver _strategyResolver;
    public VerifyOtpCommandHandler(
        IOtpService otpService,
        IUserRepository userRepository,
        OtpVerificationStrategyResolver strategyResolver)
    {
        _otpService = otpService;
        _userRepository = userRepository;
        _strategyResolver = strategyResolver;
    }

    public async Task<OtpVerifyResultDto> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        // 1. Lấy thông tin user
        var targetUser = await _userRepository.GetByEmailWithRolesAsync(request.Email);
        if (targetUser == null)
        {
            throw new BusinessRuleException("User not found");
        }
        // 2. Xắc thực mã OTP quá Redis OTP Service
        var verification = await _otpService.VerifyOtpAsync(targetUser.Email, request.Code, request.OtpPurpose);
        if (!verification.IsValid)
        {
            throw new BusinessRuleException(verification.Message);
        }
        // 3. Ủy quyền xử lý cho Strategy phù hợp
        var strategy = _strategyResolver.Resolve(request.OtpPurpose);
        return await strategy.VerifyAsync(targetUser, request, cancellationToken);
    }
}
