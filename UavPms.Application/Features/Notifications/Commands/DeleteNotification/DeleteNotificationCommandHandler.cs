using MediatR;
using UavPms.Application.Common.Exceptions;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Notifications.Commands.DeleteNotification;

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
        var n = await _notificationRepository.GetByIdAsync(request.Id);
        if (n == null)
        {
            throw new NotFoundException("Notification", request.Id);
        }
        
        await _notificationRepository.DeleteAsync(n);
        await _unitOfWork.SaveChangesAsync();
    }
}