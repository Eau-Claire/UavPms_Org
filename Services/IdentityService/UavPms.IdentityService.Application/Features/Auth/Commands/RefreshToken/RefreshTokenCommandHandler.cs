using MediatR;
using UavPms.IdentityService.Application.Common.Interfaces;
using UavPms.IdentityService.Application.Common.Utilities;
using UavPms.IdentityService.Application.Features.Auth.DTOs;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using RefreshTokenEntity = UavPms.IdentityService.Domain.Entities.RefreshToken;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    private readonly IGenericRepository<RefreshTokenEntity> _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserTokenService _userTokenService;

    public RefreshTokenCommandHandler(
        IGenericRepository<RefreshTokenEntity> refreshTokenRepository,
        IUserRepository userRepository,
        IUserTokenService userTokenService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _userTokenService = userTokenService;
    }

    public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // 1. Tìm Refresh Token chưa bị thu hồi và chưa hết hạn
        var hash = TokenHasher.Hash(request.RefreshToken);
        var token = (await _refreshTokenRepository.FindAsync(
            x => x.TokenHash == hash && x.RevokedAt == null,
            track: true)).FirstOrDefault();

        if (token == null || token.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        // 2. Tìm người dùng sở hữu token
        var user = await _userRepository.GetByIdWithRolesAsync(token.UserId);
        if (user == null || !user.IsActive())
        {
            throw new UnauthorizedAccessException("User not found or inactive");
        }

        // 3. Thu hồi token cũ (Revoke old token)
        token.RevokedAt = DateTime.UtcNow;

        // 4. Cấp phát cặp Token mới qua Reusable UserTokenService
        return await _userTokenService.IssueTokensAsync(user, request.UserAgent);
    }
}
