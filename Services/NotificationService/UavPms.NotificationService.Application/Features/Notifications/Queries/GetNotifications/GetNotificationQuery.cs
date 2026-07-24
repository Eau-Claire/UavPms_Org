using MediatR;
using UavPms.NotificationService.Application.Features.Notifications.DTOs;

namespace UavPms.NotificationService.Application.Features.Notifications.Queries.GetNotifications;

public record GetNotificationsQuery(Guid UserId) : IRequest<List<NotificationDto>>;