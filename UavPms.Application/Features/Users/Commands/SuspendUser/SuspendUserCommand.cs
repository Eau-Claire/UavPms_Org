using MediatR;

namespace UavPms.Application.Features.Users.Commands.SuspendUser;

public record SuspendUserCommand(Guid Id) : IRequest<bool>;
