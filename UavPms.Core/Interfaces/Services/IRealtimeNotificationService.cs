using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Services;

public interface IRealtimeNotificationService
{
    Task SendToUserAsync(Guid userId, Notification notification, CancellationToken cancellationToken = default);
    Task SendToUsersAsync(IEnumerable<Guid> userIds, Notification notification, CancellationToken cancellationToken = default);
    Task SendToRoleAsync(string roleName, Notification notification, CancellationToken cancellationToken = default);
}
