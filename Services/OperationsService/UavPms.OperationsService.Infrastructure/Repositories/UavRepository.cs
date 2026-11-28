using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;

namespace UavPms.OperationsService.Infrastructure.Repositories;

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

    public async Task<IReadOnlyList<Uav>> GetVisibleAsync(CancellationToken ct = default) =>
        await DroneScope.Visible(_context, _context.CurrentUser).AsNoTracking().ToListAsync(ct);

    public Task<bool> IsVisibleAsync(Guid id, CancellationToken ct = default) =>
        DroneScope.Visible(_context, _context.CurrentUser).AnyAsync(u => u.Id == id, ct);
}
