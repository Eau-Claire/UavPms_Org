using MediatR;

namespace UavPms.IdentityService.Application.Features.Users.Commands.SuspendUser;

public record SuspendUserCommand(Guid Id) : IRequest<bool>;
