using Microsoft.EntityFrameworkCore;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Infrastructure.Persistence;

namespace UavPms.Infrastructure.Repositories;

public class MissionRepository : GenericRepository<Mission>, IMissionRepository
{
    public MissionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<Mission> Items, int TotalCount)> GetMissionsPagedAsync(int page, int pageSize, string? search, string? status)
    {
        var query = _context.Missions
            .Include(m => m.AssignedToUser)
            .Include(m => m.Manager)
            .Include(m => m.Uav)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => m.Title.Contains(search) ||
                                     m.Description.Contains(search) ||
                                     m.MissionCode.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(m => m.Status == status);
        }
        
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(m  =>m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Mission>> GetMissionsByAssignedUserAsync(Guid userId)
    {
        return await _context.Missions
            .Include(m => m.AssignedToUser)
            .Include(m => m.Manager)
            .Include(m => m.Uav)
            .Where(m => m.AssignedToUserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<Mission?> GetMissionDetailsByIdAsync(Guid id)
    {
        return await _context.Missions
            .Include(m => m.AssignedToUser)
            .Include(m => m.Manager)
            .Include(m => m.Uav)
            .FirstOrDefaultAsync(m => m.Id == id);
    }
}