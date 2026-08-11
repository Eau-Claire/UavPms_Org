using UavPms.IdentityService.Application.Common.Exceptions;
using UavPms.IdentityService.Application.Common.Interfaces;
using UavPms.IdentityService.Application.Common.Utilities;
using UavPms.IdentityService.Application.Features.Auth.DTOs;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Domain.Interfaces.Repositories;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies;

public class LoginOtpStrategy : IOtpVerificationStrategy
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserTokenService _userTokenService;
    private readonly IGenericRepository<TrustedDevice> _trustedDeviceRepository;

    public LoginOtpStrategy(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IUserTokenService userTokenService,
        IGenericRepository<TrustedDevice> trustedDeviceRepository
    )
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _userTokenService = userTokenService;
        _trustedDeviceRepository = trustedDeviceRepository;
    }
    
    public bool CanHandle(OtpPurpose purpose)
        => purpose == OtpPurpose.Login || purpose == OtpPurpose.EmailVerification;

    public async Task<OtpVerifyResultDto> VerifyAsync(User user, VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        if (request.OtpPurpose == OtpPurpose.Login && !user.IsActive())
        {
            throw new BusinessRuleException("User account is not active.");
        }

        if (request.OtpPurpose == OtpPurpose.EmailVerification)
        {
            user.VerifyEmail();
            await _userRepository.UpdateAsync(user);
        }
        
        // 1. Cấp phát Authentication Tokens qua UserTokenService
        var deviceTrustToken = Guid.NewGuid().ToString("N");
        var authResult = await _userTokenService.IssueTokensAsync(user, request.UserAgent, deviceTrustToken, cancellationToken);
        
        // 2.Lưu TrustedDevice
        await _trustedDeviceRepository.AddAsync(new TrustedDevice
        {
            UserId = user.Id,
            DeviceTokenHash = TokenHasher.Hash(deviceTrustToken),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            LastUsedAt = DateTime.UtcNow,
            UserAgent = request.UserAgent ?? string.Empty
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new OtpVerifyResultDto
        {
            Success = true,
            Message = "Verification Successful",
            AuthResult = authResult
        };
    }
}