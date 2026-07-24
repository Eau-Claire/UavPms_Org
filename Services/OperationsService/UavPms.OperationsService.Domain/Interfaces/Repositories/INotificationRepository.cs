namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

using UavPms.OperationsService.Domain.Entities;

public interface INotificationRepository : IGenericRepository<Notification> {
    public Task<List<Notification>> GetByUserAsync(Guid userId);
    public Task MarkAsReadAsync(Guid id);
    public Task<List<Notification>> GetUnpushedNotificationsWithActiveUserAsync();
}