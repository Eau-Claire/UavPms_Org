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
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IGenericRepository<RefreshTokenEntity> refreshTokenRepository,
        IUserRepository userRepository,
        IUserTokenService userTokenService,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _userTokenService = userTokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // 1. Tìm Refresh Token chưa bị thu hồi và chưa hết hạn
        var hash = TokenHasher.Hash(request.RefreshToken);

        var token = (await _refreshTokenRepository.FindAsync(
            x => x.TokenHash == hash,
            track: true)).FirstOrDefault();

        if (token == null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        if(token.RevokedAt != null)
        {
            var activeTokens = await _refreshTokenRepository.FindAsync(
                x => x.UserId == token.UserId && 
                x.RevokedAt == null, track: true);
            
            foreach (var activeToken in activeTokens)
            {
                activeToken.RevokedAt = DateTime.UtcNow;
                await _refreshTokenRepository.UpdateAsync(activeToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Revoked refresh token reused. Security breach detected. All active sessions revoked.");
        }

        if (token.ExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Expired refresh token");
        }

        var user = await _userRepository.GetByIdWithRolesAsync(token.UserId);
        if (user == null || !user.IsActive())
        {
            throw new UnauthorizedAccessException("User not found or inactive");
        }

        token.RevokedAt = DateTime.UtcNow;
        
        return await _userTokenService.IssueTokensAsync(user, request.UserAgent, cancellationToken: cancellationToken);
    }
}
