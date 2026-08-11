using System.Collections.Generic;
using MediatR;
using UavPms.IdentityService.Application.Features.Users.DTOs;

namespace UavPms.IdentityService.Application.Features.Users.Queries.GetAssignableUsers;

public record GetAssignableUsersQuery : IRequest<List<AssignableUserDto>>;