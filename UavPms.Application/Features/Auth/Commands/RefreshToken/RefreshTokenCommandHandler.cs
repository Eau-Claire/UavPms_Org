using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Configuration;
using UavPms.Application.Features.Auth.DTOs;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using RefreshTokenEntity = UavPms.Core.Entities.RefreshToken;

namespace UavPms.Application.Features.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    private readonly IGenericRepository<RefreshTokenEntity> _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IGenericRepository<RefreshTokenEntity> refreshTokenRepository,
        IUserRepository userRepository,
        IJwtProvider jwtProvider,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _jwtProvider = jwtProvider;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = HashToken(request.RefreshToken);
        var token = (await _refreshTokenRepository.FindAsync(
            x => x.TokenHash == hash && x.RevokedAt == null,
            track: true)).FirstOrDefault();
        if (token == null || token.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }
        
        var user = await _userRepository.GetByIdWithRolesAsync(token.UserId);
        if (user == null || user.Status != "Active")
        {
            throw new UnauthorizedAccessException("User not found or inactive");
        }
        
        token.RevokedAt = DateTime.UtcNow;
        var roles = user.UserRoles.Select(r => r.Role!.RoleName).ToList();
        var newAccess = _jwtProvider.GenerateAccessToken(user, roles);
        var newRefresh =  _jwtProvider.GenerateRefreshToken();

        await _refreshTokenRepository.AddAsync(new RefreshTokenEntity
        {
            UserId = user.Id,
            TokenHash = HashToken(newRefresh),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceInfo = request.UserAgent ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
        });
        
        await _unitOfWork.SaveChangesAsync();

        var userDto = new AuthUserDto
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            FullName = user.FullName,
            Roles = roles,
        };
        
        return AuthResultDto.SuccessResult(newAccess, newRefresh, 0, userDto);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.Create().ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}