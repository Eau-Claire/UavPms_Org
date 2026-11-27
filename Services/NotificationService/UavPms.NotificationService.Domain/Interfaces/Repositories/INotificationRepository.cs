namespace UavPms.NotificationService.Domain.Interfaces.Repositories;

using UavPms.NotificationService.Domain.Entities;

public interface INotificationRepository : IGenericRepository<Notification> {
    public Task<List<Notification>> GetByUserAsync(Guid userId);
    public Task<int> GetUnreadCountByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    public Task<Notification?> GetByOwnerAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    public Task<List<Notification>> GetUnpushedNotificationsWithActiveUserAsync();
}
