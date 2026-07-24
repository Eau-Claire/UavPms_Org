using Microsoft.EntityFrameworkCore;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Infrastructure.Persistence;

namespace UavPms.AIInspectionService.Infrastructure.Repositories;

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