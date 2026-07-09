using MediatR;
using UavPms.Application.Common.Exceptions;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Notifications.Commands.MarkNotificationAsRead;

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
        var n = await _notificationRepository.GetByIdAsync(request.Id);
        if (n == null)
        {
            throw new NotFoundException("Notification", request.Id);    
        }
        
        await _notificationRepository.MarkAsReadAsync(n.Id);
        await _unitOfWork.SaveChangesAsync();
    }
}