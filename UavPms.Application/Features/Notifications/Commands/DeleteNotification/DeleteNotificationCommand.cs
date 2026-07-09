using MediatR;

namespace UavPms.Application.Features.Notifications.Commands.DeleteNotification;

public record DeleteNotificationCommand (Guid Id) : IRequest;