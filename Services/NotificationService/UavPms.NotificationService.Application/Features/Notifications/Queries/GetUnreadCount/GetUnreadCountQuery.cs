using MediatR;

namespace UavPms.NotificationService.Application.Features.Notifications.Queries.GetUnreadCount;

public record GetUnreadCountQuery(Guid UserId) : IRequest<int>;

