using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Configuration;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.Auth.DTOs;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using RefreshTokenEntity = UavPms.Core.Entities.RefreshToken;
using UavPms.Core.Entities;

namespace UavPms.Application.Features.Auth.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, OtpVerifyResultDto>
{
    private readonly IOtpService _otpService;
    private readonly IUserRepository _userRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IGenericRepository<RefreshTokenEntity> _refreshTokenRepository;
    private readonly IGenericRepository<TrustedDevice> _trustedDeviceRepository;
    public VerifyOtpCommandHandler(
        IOtpService otpService,
        IUserRepository userRepository,
        IJwtProvider jwtProvider,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IGenericRepository<RefreshTokenEntity> refreshTokenRepository,
        IGenericRepository<TrustedDevice> trustedDeviceRepository)
    {
        _otpService = otpService;
        _userRepository = userRepository;
        _jwtProvider = jwtProvider;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _refreshTokenRepository = refreshTokenRepository;
        _trustedDeviceRepository = trustedDeviceRepository;
    }

    public async Task<OtpVerifyResultDto> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var emailUser = await _userRepository.GetByEmailWithRolesAsync(request.Email);
        var targetUser = request.OtpPurpose == OtpPurpose.Login
            ? emailUser
            : emailUser ?? await _userRepository.GetByUsernameWithRolesAsync(request.Email);
        var targetEmail = targetUser?.Email ?? request.Email;

        var verification = await _otpService.VerifyOtpAsync(targetEmail, request.Code, request.OtpPurpose);
        if (!verification.IsValid)
        {
            throw new BusinessRuleException(verification.Message);
        }

        var resultDto = new OtpVerifyResultDto
        {
            Success = true,
            Message = "Verification Successful",
        };

        if (request.OtpPurpose == OtpPurpose.Login || request.OtpPurpose == OtpPurpose.EmailVerification)
        {
            var user = targetUser;
            if (user == null)
            {
                throw new NotFoundException("User not found", request.Email);
            }

            if (request.OtpPurpose == OtpPurpose.Login && user.Status != "Active")
            {
                throw new BusinessRuleException("User account is not active.");
            }

            if (request.OtpPurpose == OtpPurpose.EmailVerification)
            {
                user.IsEmailVerified = true;
                user.Status = "Active";
                await _userRepository.UpdateAsync(user);
            }

            var roles = user.UserRoles.Select(r => r.Role!.RoleName).ToList();
            var accessToken = _jwtProvider.GenerateAccessToken(user, roles);
            var refreshToken = _jwtProvider.GenerateRefreshToken();
            var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var m) ? m : 60;

            await _refreshTokenRepository.AddAsync(new RefreshTokenEntity
            {
                UserId = user.Id,
                TokenHash = HashToken(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                DeviceInfo = request.UserAgent ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
            });

            var deviceTrustToken = Guid.NewGuid().ToString("N");
            var tokenHash = HashToken(deviceTrustToken);
            await _trustedDeviceRepository.AddAsync(new TrustedDevice
            {
                UserId = user.Id,
                DeviceTokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                LastUsedAt = DateTime.UtcNow,
                UserAgent = request.UserAgent ?? string.Empty
            });

            await _unitOfWork.SaveChangesAsync();

            resultDto.AuthResult = AuthResultDto.SuccessResult(
                accessToken,
                refreshToken,
                expiryMinutes * 60,
                new AuthUserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Username = user.Username,
                    FullName = user.FullName,
                    Roles = roles,
                },
                deviceTrustToken);
        }
        else if (request.OtpPurpose == OtpPurpose.ForgotPassword)
        {
            var token = Guid.NewGuid().ToString();
            var hash = HashToken(token);
            await _otpService.SaveVerificationTokenAsync(hash, targetEmail, TimeSpan.FromMinutes(10));
            resultDto.Token = token;
        }
        else if (request.OtpPurpose == OtpPurpose.ChangePassword ||
                 request.OtpPurpose == OtpPurpose.ChangeEmail ||
                 request.OtpPurpose == OtpPurpose.DeleteAccount)
        {
            var user = targetUser;

            if (user == null || user.Status != "Active")
            {
                throw new NotFoundException("User not found", request.Email);
            }
            
            var token = _jwtProvider.GenerateStepUpToken(user, request.OtpPurpose.ToString());
            await _otpService.SaveStepUpTokenAsync(user.Id.ToString(), 
                request.OtpPurpose.ToString(), token, TimeSpan.FromMinutes(5));
            
            resultDto.Token = token;
        }
        return resultDto;
    }
    
    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}
