using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Configuration;
using UavPms.IdentityService.Application.Common.Exceptions;
using UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies;
using UavPms.IdentityService.Application.Features.Auth.DTOs;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Domain.Interfaces.Services;
using RefreshTokenEntity = UavPms.IdentityService.Domain.Entities.RefreshToken;
using UavPms.IdentityService.Domain.Entities;

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
        vả
        // 2. Xắc thực mã OTP quá Redis OTP Service
        
        // 3. Ủy quyền xử lý cho Strategy phù hợp
    }
}
