using UavPms.IdentityService.Application.Features.Auth.DTOs;
using UavPms.IdentityService.Domain.Entities;
namespace UavPms.IdentityService.Application.Common.Interfaces;

/// <summary>
/// Reusable Component dùng để quản lý việc cấp phát Authentication Tokens (Access + Refresh)
/// và Session cho user
/// </summary>
public interface IUserTokenService
{
    /// <summary>
    /// Sinh AccessToken & RefreshToken, lưu RFToken vào Db và trả về AuthResultDto
    /// </summary>
    Task<AuthResultDto> IssueTokensAsync(
        User user,
        string? userAgent,
        string? deviceTrustToken = null);
}