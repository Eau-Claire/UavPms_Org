using Microsoft.EntityFrameworkCore;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Infrastructure.Persistence;

namespace UavPms.Infrastructure.Repositories;

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