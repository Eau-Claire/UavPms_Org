using MediatR;
using UavPms.Application.Features.Notifications.DTOs;

namespace UavPms.Application.Features.Notifications.Commands.CreateNotification;

public record CreateNotificationCommand(
    Guid UserId,
    string Type,
    string? ReferenceType,
    Guid? ReferenceId,
    string Title,
    string Body
) : IRequest<NotificationDto>;