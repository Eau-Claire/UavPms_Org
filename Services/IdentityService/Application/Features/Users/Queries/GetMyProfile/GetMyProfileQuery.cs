using MediatR;
using UavPms.IdentityService.Application.Features.Auth.DTOs;

namespace UavPms.IdentityService.Application.Features.Users.Queries.GetMyProfile;

public record GetMyProfileQuery(Guid UserId) : IRequest<AuthUserDto>;

