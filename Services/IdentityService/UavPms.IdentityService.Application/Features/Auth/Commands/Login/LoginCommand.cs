using MediatR;
using UavPms.IdentityService.Application.Features.Auth.DTOs;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.Login;

public record LoginCommand(
    string Email,
    string Password,
    string? DeviceTrustToken,
    string? UserAgent
) : IRequest<AuthResultDto>;

