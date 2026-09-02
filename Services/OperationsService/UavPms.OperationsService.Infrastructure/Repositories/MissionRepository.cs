using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class MissionRepository : GenericRepository<Mission>, IMissionRepository
{
    public MissionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<Mission> Items, int TotalCount)> GetMissionsPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        string? sortBy = "createdAt",
        bool sortDescending = true)
    {
        var query = _context.Missions
            .Include(m => m.Inspector)
            .Include(m => m.Manager)
            .Include(m => m.Uav)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => m.Title.Contains(search) ||
                                     m.Description.Contains(search) ||
                                     m.MissionCode.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<MissionStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(m => m.Status == parsedStatus);
        }

        
        query = ApplySorting(query, sortBy, sortDescending);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (items, totalCount);
    }

    private static IQueryable<Mission> ApplySorting(
        IQueryable<Mission> query,
        string? sortBy,
        bool sortDescending)
    {
        var normalizedSortBy = (sortBy ?? "createdAt").Trim().ToLowerInvariant();

        return normalizedSortBy switch
        {
            "title" => sortDescending
                ? query.OrderByDescending(m => m.Title)
                : query.OrderBy(m => m.Title),
            "status" => sortDescending
                ? query.OrderByDescending(m => m.Status)
                : query.OrderBy(m => m.Status),
            "missioncode" or "mission_code" => sortDescending
                ? query.OrderByDescending(m => m.MissionCode)
                : query.OrderBy(m => m.MissionCode),
            _ => sortDescending
                ? query.OrderByDescending(m => m.CreatedAt)
                : query.OrderBy(m => m.CreatedAt)
        };
    }

    public async Task<IReadOnlyList<Mission>> GetMissionsByAssignedUserAsync(Guid userId)
    {
        return await _context.Missions
            .Include(m => m.Inspector)
            .Include(m => m.Manager)
            .Include(m => m.Uav)
            .Where(m => m.InspectorId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<Mission?> GetMissionDetailsByIdAsync(Guid id)
    {
        return await _context.Missions
            .Include(m => m.Inspector)
            .Include(m => m.Manager)
            .Include(m => m.Uav)
            .Include(m => m.MissionTargets)
                .ThenInclude(t => t.Asset)
                    .ThenInclude(a => a!.PowerLine)
            .Include(m => m.MissionTargets)
                .ThenInclude(t => t.Asset)
                    .ThenInclude(a => a!.Tower)
            .FirstOrDefaultAsync(m => m.Id == id);
    }
}
