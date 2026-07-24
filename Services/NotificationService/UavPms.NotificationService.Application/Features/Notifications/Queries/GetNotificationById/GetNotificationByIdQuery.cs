using MediatR;
using UavPms.NotificationService.Application.Features.Notifications.DTOs;

namespace UavPms.NotificationService.Application.Features.Notifications.Queries.GetNotificationById;

public record GetNotificationByIdQuery(Guid Id) : IRequest<NotificationDto>;
