using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Domain.Contracts;

namespace UavPms.NotificationService.Domain.Interfaces.Services;

public interface IRealtimeNotificationService
{
    Task SendToUserAsync(Guid userId, Notification notification, CancellationToken cancellationToken = default);
    Task SendToUsersAsync(IEnumerable<Guid> userIds, Notification notification, CancellationToken cancellationToken = default);
    Task SendToRoleAsync(string roleName, Notification notification, CancellationToken cancellationToken = default);
    Task SendAiAnalysisStatusToUserAsync(Guid userId, AIAnalysisStatusChangedEvent statusChanged, CancellationToken cancellationToken = default);
}
