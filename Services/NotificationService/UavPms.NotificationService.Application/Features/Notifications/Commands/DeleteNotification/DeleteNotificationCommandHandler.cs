using MediatR;
using UavPms.NotificationService.Application.Common.Exceptions;
using UavPms.NotificationService.Domain.Interfaces.Repositories;

namespace UavPms.NotificationService.Application.Features.Notifications.Commands.DeleteNotification;

public class DeleteNotificationCommandHandler : IRequestHandler<DeleteNotificationCommand>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteNotificationCommandHandler(
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        var n = await _notificationRepository.GetByOwnerAsync(request.Id, request.UserId, cancellationToken);
        if (n == null)
        {
            throw new NotFoundException("Notification was not found.");
        }
        
        await _notificationRepository.DeleteAsync(n);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
