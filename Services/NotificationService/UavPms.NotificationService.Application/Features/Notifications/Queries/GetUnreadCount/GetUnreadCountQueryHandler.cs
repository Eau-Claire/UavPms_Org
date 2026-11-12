using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.NotificationService.Domain.Interfaces.Repositories;

namespace UavPms.NotificationService.Application.Features.Notifications.Queries.GetUnreadCount;

public class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, int>
{
    private readonly INotificationRepository _notificationRepository;

    public GetUnreadCountQueryHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public Task<int> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
        => _notificationRepository.GetUnreadCountByUserAsync(request.UserId, cancellationToken);
}

