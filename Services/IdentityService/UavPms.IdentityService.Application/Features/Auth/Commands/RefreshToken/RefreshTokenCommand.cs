using MediatR;
using UavPms.IdentityService.Application.Features.Auth.DTOs;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(
    string RefreshToken,
    string? UserAgent
) : IRequest<AuthResultDto>;