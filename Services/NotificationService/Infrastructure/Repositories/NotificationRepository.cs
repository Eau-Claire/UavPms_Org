namespace UavPms.NotificationService.Infrastructure.Repositories;

using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Domain.Interfaces.Repositories;
using UavPms.NotificationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
{
    public NotificationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<List<Notification>> GetByUserAsync(Guid userId)
    {
        return await _context.Notifications
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.SentAt)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var notification = await _context.Notifications.FindAsync(id);

        if(notification == null)
            return;

        notification.IsRead = true;
    }

    public async Task<List<Notification>> GetUnpushedNotificationsWithActiveUserAsync()
    {
        return await _context.Notifications
            .Include(x => x.User)
            .Where(x => !x.IsPushed)
            .ToListAsync();
    }
}