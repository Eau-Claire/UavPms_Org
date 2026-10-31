using MediatR;

namespace UavPms.NotificationService.Application.Features.Notifications.Commands.MarkNotificationAsRead;

/// <param name="UserId">The caller; when set, only that user's own notification can be marked read.</param>
public record MarkNotificationAsReadCommand(Guid Id, Guid? UserId = null) : IRequest;
