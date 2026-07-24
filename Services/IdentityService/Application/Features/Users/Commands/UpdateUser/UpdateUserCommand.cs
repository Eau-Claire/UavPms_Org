using MediatR;
using System;
using System.Collections.Generic;

namespace UavPms.IdentityService.Application.Features.Users.Commands.UpdateUser;

public record UpdateUserCommand(
    Guid Id,
    string Email,
    string FullName,
    string Phone,
    string Status,
    List<string> Roles
) : IRequest<bool>;
