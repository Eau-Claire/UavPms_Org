using MediatR;
using System;
using System.Collections.Generic;

namespace UavPms.Application.Features.Users.Commands.CreateUser;

public record CreateUserCommand(
    string Username,
    string Email,
    string Password,
    string FullName,
    string Phone,
    List<string> Roles
) : IRequest<Guid>;
    