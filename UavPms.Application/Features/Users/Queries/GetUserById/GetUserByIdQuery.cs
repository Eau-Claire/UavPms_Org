using MediatR;
using UavPms.Application.Features.Users.DTOs;

namespace UavPms.Application.Features.Users.Queries.GetUserById;

public record GetUserByIdQuery(Guid Id) : IRequest<UserDetailDto>;