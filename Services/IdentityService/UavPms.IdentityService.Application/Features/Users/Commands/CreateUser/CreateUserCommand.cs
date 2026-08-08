using MediatR;
using System;
using System.Collections.Generic;

namespace UavPms.IdentityService.Application.Features.Users.Commands.CreateUser;

public record CreateUserCommand(
    string Email,
    string Password,
    string FullName,
    string Phone,
    List<string> Roles
) : IRequest<Guid>;
    