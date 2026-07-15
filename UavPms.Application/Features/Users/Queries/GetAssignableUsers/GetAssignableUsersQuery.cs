using System.Collections.Generic;
using MediatR;
using UavPms.Application.Features.Users.DTOs;

namespace UavPms.Application.Features.Users.Queries.GetAssignableUsers;

public record GetAssignableUsersQuery : IRequest<List<AssignableUserDto>>;