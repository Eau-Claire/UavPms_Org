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
        bool sortDescending = true,
        string? priority = null,
        IReadOnlyList<Guid?>? allowedRegionIds = null)
    {
        var query = _context.Missions
            .Include(m => m.Inspector)
            .Include(m => m.Manager)
            .AsQueryable();

        if (allowedRegionIds != null && !allowedRegionIds.Contains(null))
        {
            query = query.Where(m => m.RegionId.HasValue && allowedRegionIds.Contains(m.RegionId.Value));
        }

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

        if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<MissionPriority>(priority, true, out var parsedPriority))
        {
            query = query.Where(m => m.Priority == parsedPriority);
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
            "priority" => sortDescending
                ? query.OrderByDescending(m => m.Priority)
                       .ThenBy(m => m.PlannedStart)
                       .ThenBy(m => m.CreatedAt)
                : query.OrderBy(m => m.Priority)
                       .ThenBy(m => m.PlannedStart)
                       .ThenBy(m => m.CreatedAt),
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
            .AsNoTracking()
            .Include(m => m.Inspector)
            .Include(m => m.Manager)
            .Include(m => m.Uav)
            .Include(m => m.Region)
            .Include(m => m.Assignments.Where(a => a.Status == MissionAssignmentStatus.Active)).ThenInclude(a => a.User)
            .Where(m => (m.InspectorId.HasValue && m.InspectorId.Value == userId) || m.Assignments.Any(a => a.UserId == userId && a.Status == MissionAssignmentStatus.Active) || m.ManagerId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<Mission?> GetMissionDetailsByIdAsync(Guid id)
    {
        return await _context.Missions
            .AsSplitQuery()
            .Include(m => m.Inspector)
            .Include(m => m.Manager)
            .Include(m => m.Uav)
            .Include(m => m.Region)
            .Include(m => m.Schedule)
            .Include(m => m.Assignments).ThenInclude(a => a.User)
            .Include(m => m.CheckIns)
            .Include(m => m.DroneHandovers)
            .Include(m => m.MissionTargets)
                .ThenInclude(t => t.Asset)
                    .ThenInclude(a => a!.PowerLine)
            .Include(m => m.MissionTargets)
                .ThenInclude(t => t.Asset)
                    .ThenInclude(a => a!.Tower)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public Task<bool> UserCanAccessAsync(Guid missionId, Guid userId, bool global, CancellationToken cancellationToken)
    {
        if (global) return Task.FromResult(true);
        return _context.Missions.AnyAsync(m => m.Id == missionId &&
            (m.ManagerId == userId || (m.InspectorId.HasValue && m.InspectorId.Value == userId) || m.Assignments.Any(a => a.UserId == userId && a.Status == MissionAssignmentStatus.Active)
             || _context.UserGeographicScopes.Any(s => s.UserId == userId && s.RegionId == m.RegionId)), cancellationToken);
    }

    public Task<bool> UserCanManageAsync(Guid missionId, Guid userId, bool global, CancellationToken cancellationToken)
    {
        if (global) return Task.FromResult(true);
        return _context.Missions.AnyAsync(m => m.Id == missionId &&
            _context.UserGeographicScopes.Any(s => s.UserId == userId && s.RegionId == m.RegionId), cancellationToken);
    }
}
