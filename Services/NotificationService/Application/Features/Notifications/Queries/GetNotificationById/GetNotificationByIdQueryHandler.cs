using MediatR;
using UavPms.NotificationService.Application.Common.Exceptions;
using UavPms.NotificationService.Application.Features.Notifications.DTOs;
using UavPms.NotificationService.Domain.Interfaces.Repositories;

namespace UavPms.NotificationService.Application.Features.Notifications.Queries.GetNotificationById;

public class GetNotificationByIdQueryHandler : IRequestHandler<GetNotificationByIdQuery, NotificationDto>
{
    private readonly INotificationRepository _notificationRepository;

    public GetNotificationByIdQueryHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<NotificationDto> Handle(GetNotificationByIdQuery request, CancellationToken cancellationToken)
    {
        var n = await _notificationRepository.GetByIdAsync(request.Id);
        if (n == null)
        {
            throw new NotFoundException("Notification", request.Id);
        }

        return new NotificationDto
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
            ReadAt = n.ReadAt,
        };
    }
}