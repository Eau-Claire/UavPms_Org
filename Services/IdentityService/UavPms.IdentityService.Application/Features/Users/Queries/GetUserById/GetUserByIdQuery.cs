using MediatR;
using UavPms.IdentityService.Application.Features.Users.DTOs;

namespace UavPms.IdentityService.Application.Features.Users.Queries.GetUserById;

public record GetUserByIdQuery(Guid Id) : IRequest<UserDetailDto>;