using MediatR;
using UavPms.NotificationService.Application.Features.Notifications.DTOs;
using UavPms.NotificationService.Domain.Interfaces.Repositories;

namespace UavPms.NotificationService.Application.Features.Notifications.Queries.GetNotifications;

public class GetNotificationQueryHandler : IRequestHandler<GetNotificationsQuery, List<NotificationDto>>
{
    private readonly INotificationRepository _notificationRepository;

    public GetNotificationQueryHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }
    
    public async Task<List<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var list = await _notificationRepository.GetByUserAsync(request.UserId);
        
        return list.Select(n => new NotificationDto
        {
            Id = n.Id,
            UserId = n.UserId,
            Type = n.Type,
            ReferenceType = n.ReferenceType,
            ReferenceId = n.ReferenceId,
            Title = n.Title,
            Body = n.Body,
            IsRead = n.IsRead,
            SentAt = n.SentAt,
            ReadAt = n.ReadAt
        }).ToList();
    }
}