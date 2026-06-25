using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Configuration;
using UavPms.Application.Features.Auth.DTOs;
using UavPms.Core.Entities;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using RefreshTokenEntity = UavPms.Core.Entities.RefreshToken;

namespace UavPms.Application.Features.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResultDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IGenericRepository<RefreshTokenEntity> _refreshTokenRepository;
    private readonly IOtpService _otpService;
    private readonly IGenericRepository<TrustedDevice>  _trustedDeviceRepository;
    private const string DummyHash = "$2a$10$vI8aWBZdKeu5JcGlZtu4U.25m68c9c61234567890123456789012";

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider,
        IGenericRepository<RefreshTokenEntity> refreshTokenRepository,
        IOtpService otpService,
        IGenericRepository<TrustedDevice> trustedDeviceRepository,
        IConfiguration configuration,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
        _refreshTokenRepository = refreshTokenRepository;
        _otpService = otpService;
        _trustedDeviceRepository = trustedDeviceRepository;
        _configuration = configuration;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailWithRolesAsync(request.Email) 
            ?? await _userRepository.GetByUsernameWithRolesAsync(request.Email);

        bool isValidUser = true;
        string passwordHashToVerify = DummyHash;

        if (user == null || user.Status != "Active")
        {
            isValidUser = false;
        }
        else 
        {
            passwordHashToVerify = user.PasswordHash;
        }

        bool passwordMatch = _passwordHasher.Verify(passwordHashToVerify, request.Password);

        if (!isValidUser || !passwordMatch)
        {
            isValidUser = false;
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        if (!user!.IsEmailVerified)
        {
            throw new UnauthorizedAccessException("Email not verified");
        }

        var trustedDevice = await GetValidTrustedDeviceAsync(user.Id, request.DeviceTrustToken);

        if (trustedDevice != null)
        {
            trustedDevice.LastUsedAt = DateTime.UtcNow;
            trustedDevice.ExpiresAt = DateTime.UtcNow.AddDays(30);
            await _trustedDeviceRepository.UpdateAsync(trustedDevice);
            return await IssueAuthenticationResponseAsync(user, request.UserAgent, request.DeviceTrustToken);
        }

        var otp = await _otpService.GenerateAndSendOtpAsync(user.Email, OtpPurpose.Login);
        if (!otp.Success)
        {
            throw new Exception(otp.Message);
        }

        return AuthResultDto.OtpRequiredResult(user.Email);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.Create().ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    private async Task<TrustedDevice?> GetValidTrustedDeviceAsync(Guid userId, string? deviceToken)
    {
        if (string.IsNullOrEmpty(deviceToken)) return null;

        var hash = HashToken(deviceToken);
        
        var devices = await _trustedDeviceRepository.FindAsync(
            d => d.UserId == userId && d.DeviceTokenHash == hash && 
                 d.ExpiresAt > DateTime.UtcNow, track: true);
        
        return devices.FirstOrDefault();
    }

    private async Task<AuthResultDto> IssueAuthenticationResponseAsync(User user, string? userAgent, string? deviceTrustToken = null)
    {
        var roles = user.UserRoles.Select(r => r.Role!.RoleName).ToList();
        var accessToken = _jwtProvider.GenerateAccessToken(user, roles);
        var refreshToken = _jwtProvider.GenerateRefreshToken();
        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var m) ? m : 60;

        var refreshTokenEntity = new RefreshTokenEntity
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceInfo = userAgent ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
        };

        await _refreshTokenRepository.AddAsync(refreshTokenEntity);
        await _unitOfWork.SaveChangesAsync();

        var userDto = new AuthUserDto
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            FullName = user.FullName,
            Roles = roles,
        };
        
        return AuthResultDto.SuccessResult(accessToken, refreshToken, expiryMinutes * 60, userDto, deviceTrustToken);
    }
}