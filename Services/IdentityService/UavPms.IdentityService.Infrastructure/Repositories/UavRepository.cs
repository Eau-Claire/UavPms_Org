using Microsoft.EntityFrameworkCore;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Infrastructure.Persistence;

namespace UavPms.IdentityService.Infrastructure.Repositories;

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