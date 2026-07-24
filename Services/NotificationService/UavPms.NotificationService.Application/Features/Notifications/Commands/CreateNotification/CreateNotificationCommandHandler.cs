using MediatR;
using UavPms.NotificationService.Application.Features.Notifications.DTOs;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Domain.Interfaces.Repositories;
using UavPms.NotificationService.Domain.Interfaces.Services;

namespace UavPms.NotificationService.Application.Features.Notifications.Commands.CreateNotification;

public class CreateNotificationCommandHandler : IRequestHandler<CreateNotificationCommand, NotificationDto>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotificationService _realtimeNotificationService;

    public CreateNotificationCommandHandler(
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        IRealtimeNotificationService realtimeNotificationService)
    {
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _realtimeNotificationService = realtimeNotificationService;
    }
    
    public async Task<NotificationDto> Handle(CreateNotificationCommand request, CancellationToken cancellationToken)
    {
        var n = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Type = request.Type,
            ReferenceType = request.ReferenceType ?? string.Empty,
            ReferenceId = request.ReferenceId,
            Title = request.Title,
            Body = request.Body,
            IsRead = false,
            SentAt = DateTime.UtcNow
        };

        await _notificationRepository.AddAsync(n);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _realtimeNotificationService.SendToUserAsync(n.UserId, n, cancellationToken);

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
            SentAt = n.SentAt
        };
    }
}