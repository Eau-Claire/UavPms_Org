using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.IdentityService.Application.Common.Interfaces;
using UavPms.IdentityService.Application.Common.Utilities;
using UavPms.IdentityService.Application.Features.Auth.DTOs;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Domain.Interfaces.Services;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResultDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserTokenService _userTokenService;
    private readonly IOtpService _otpService;
    private readonly IGenericRepository<TrustedDevice> _trustedDeviceRepository;
    private readonly ILogger<LoginCommandHandler> _logger;

    private const string DummyHash = "$2a$10$vI8aWBZdKeu5JcGlZtu4U.25m68c9c61234567890123456789012";

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUserTokenService userTokenService,
        IOtpService otpService,
        IGenericRepository<TrustedDevice> trustedDeviceRepository,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _userTokenService = userTokenService;
        _otpService = otpService;
        _trustedDeviceRepository = trustedDeviceRepository;
        _logger = logger;
    }

    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Lấy thông tin người dùng theo email
        var user = await _userRepository.GetByEmailWithRolesAsync(request.Email);

        // 2. Kiểm tra mật khẩu (Xử lý chống Timing Attack nếu account không tồn tại hoặc inactive)
        var isPasswordValid = false;
        if (user != null && user.Status == "Active" && !string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            isPasswordValid = _passwordHasher.Verify(user.PasswordHash, request.Password);
        }
        else
        {
            // Chạy hash giả lập để đảm bảo thời gian phản hồi bằng nhau (chống Timing Attack)
            _passwordHasher.Verify(DummyHash, request.Password);
        }

        if (user == null || !isPasswordValid)
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // 3. Kiểm tra xác thực email
        if (!user.IsEmailVerified)
        {
            throw new UnauthorizedAccessException("Email not verified");
        }

        // 4. Kiểm tra xem thiết bị này đã được tin cậy chưa (Trusted Device)
        var trustedDevice = await GetValidTrustedDeviceAsync(user.Id, request.DeviceTrustToken);

        if (trustedDevice != null)
        {
            trustedDevice.LastUsedAt = DateTime.UtcNow;
            trustedDevice.ExpiresAt = DateTime.UtcNow.AddDays(30);
            await _trustedDeviceRepository.UpdateAsync(trustedDevice);

            // Cấp phát Token trực tiếp nếu là thiết bị tin cậy
            return await _userTokenService.IssueTokensAsync(user, request.UserAgent, request.DeviceTrustToken);
        }

        // 5. Nếu không phải thiết bị tin cậy -> Gửi OTP yêu cầu đăng nhập
        var otp = await _otpService.GenerateAndSendOtpAsync(user.Email, OtpPurpose.Login);
        if (!otp.Success)
        {
            throw new Exception(otp.Message);
        }

        return AuthResultDto.OtpRequiredResult(user.Email);
    }

    private async Task<TrustedDevice?> GetValidTrustedDeviceAsync(Guid userId, string? deviceToken)
    {
        if (string.IsNullOrEmpty(deviceToken)) return null;

        var hash = TokenHasher.Hash(deviceToken);

        var devices = await _trustedDeviceRepository.FindAsync(
            d => d.UserId == userId && 
                 d.DeviceTokenHash == hash && 
                 d.ExpiresAt > DateTime.UtcNow, 
            track: true);

        return devices.FirstOrDefault();
    }
}
