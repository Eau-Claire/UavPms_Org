using Microsoft.EntityFrameworkCore;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Domain.Interfaces.Repositories;
using UavPms.NotificationService.Infrastructure.Persistence;

namespace UavPms.NotificationService.Infrastructure.Repositories;

public class UavRepository : GenericRepository<Uav>, IUavRepository
{
    public UavRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Uav?> GetByUavCodeAsync(string code)
    {
        return await _context.Uavs
            .FirstOrDefaultAsync(u => u.UavCode == code);
    }
}