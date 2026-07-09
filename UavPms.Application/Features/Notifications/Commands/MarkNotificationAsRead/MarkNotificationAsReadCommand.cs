using MediatR;

namespace UavPms.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public record MarkNotificationAsReadCommand(Guid Id) : IRequest;
