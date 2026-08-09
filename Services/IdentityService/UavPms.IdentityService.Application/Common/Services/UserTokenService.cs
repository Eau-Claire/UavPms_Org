using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using UavPms.IdentityService.Application.Common.Interfaces;
using UavPms.IdentityService.Application.Common.Utilities;
using UavPms.IdentityService.Application.Features.Auth.DTOs;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Domain.Interfaces.Services;
using RefreshTokenEntity = UavPms.IdentityService.Domain.Entities.RefreshToken;

namespace UavPms.IdentityService.Application.Common.Services;

public class UserTokenService : IUserTokenService
{
    private readonly IJwtProvider _jwtProvider;
    private readonly IGenericRepository<RefreshTokenEntity> _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public UserTokenService(
        IJwtProvider jwtProvider,
        IGenericRepository<RefreshTokenEntity> refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _jwtProvider = jwtProvider;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task<AuthResultDto> IssueTokensAsync(User user, string? userAgent, string? deviceTrustToken = null)
    {
        // 1. Trích xuất danh sách Roles của user
        var roles = user.UserRoles?.Select(r => r.Role!.RoleName).ToList() ?? new();

        // 2. Sinh Access Token và Refresh Token
        var accessToken = _jwtProvider.GenerateAccessToken(user, roles);
        var refreshToken = _jwtProvider.GenerateRefreshToken();

        // 3. Đọc thời gian hết hạn AccessToken từ config (mặc định 60 phút)
        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var m) ? m : 60;

        // 4. Tạo entity RefreshToken & Băm RefreshToken bằng Pure Utility TokenHasher
        var refreshTokenEntity = new RefreshTokenEntity
        {
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceInfo = userAgent ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
        };

        await _refreshTokenRepository.AddAsync(refreshTokenEntity);
        await _unitOfWork.SaveChangesAsync();

        // 5. Đóng gói DTO kết quả
        var userDto = new AuthUserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Roles = roles,
        };

        return AuthResultDto.SuccessResult(accessToken, refreshToken, expiryMinutes * 60, userDto, deviceTrustToken);
    }
}
