namespace UavPms.NotificationService.Domain.Interfaces.Repositories;

using UavPms.NotificationService.Domain.Entities;

public interface INotificationRepository : IGenericRepository<Notification> {
    public Task<List<Notification>> GetByUserAsync(Guid userId);
    public Task<int> GetUnreadCountByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    public Task MarkAsReadAsync(Guid id);
    public Task<List<Notification>> GetUnpushedNotificationsWithActiveUserAsync();
}