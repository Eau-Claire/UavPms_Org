using MediatR;

namespace UavPms.NotificationService.Application.Features.Notifications.Commands.DeleteNotification;

public record DeleteNotificationCommand (Guid Id) : IRequest;