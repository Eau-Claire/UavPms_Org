using MediatR;
using UavPms.Application.Features.Auth.DTOs;

namespace UavPms.Application.Features.Users.Queries.GetMyProfile;

public record GetMyProfileQuery(Guid UserId) : IRequest<AuthUserDto>;

