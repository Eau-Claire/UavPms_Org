using MediatR;
using UavPms.NotificationService.Application.Common.Exceptions;
using UavPms.NotificationService.Domain.Interfaces.Repositories;

namespace UavPms.NotificationService.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MarkNotificationAsReadCommandHandler(
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var n = await _notificationRepository.GetByOwnerAsync(request.Id, request.UserId, cancellationToken);
        if (n == null)
        {
            throw new NotFoundException("Notification was not found.");
        }
        
        n.MarkAsRead();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
