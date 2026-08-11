using MediatR;
using UavPms.NotificationService.Application.Features.Notifications.DTOs;

namespace UavPms.NotificationService.Application.Features.Notifications.Commands.CreateNotification;

public record CreateNotificationCommand(
    Guid UserId,
    string Type,
    string? ReferenceType,
    Guid? ReferenceId,
    string Title,
    string Body
) : IRequest<NotificationDto>;