using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Infrastructure.Authorization;

/// <summary>
/// Applies the single GIS access rule in SQL: default geographic scope (region) OR an active
/// assignment.  It intentionally returns IQueryable so callers cannot accidentally
/// fetch the whole map and filter it in memory.
/// </summary>
public sealed class GeographicAccessFilter
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserServices? _currentUser;

    public GeographicAccessFilter(ApplicationDbContext context, ICurrentUserServices? currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private bool IsSystemAdmin => _currentUser?.IsAuthenticated == true && _currentUser.Roles != null
        && _currentUser.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase);

    private bool IsAnonymous => _currentUser is null || !_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty;

    // Spec §4: mọi role trừ SystemAdmin chỉ thấy vùng được gán. Manager chưa được gán vùng không còn thấy toàn bộ bản đồ.

    /// <summary>Region ids directly granted to the user, plus their direct child regions.</summary>
    private IQueryable<Guid> ScopedRegionIds(Guid userId)
    {
        var granted = _context.UserGeographicScopes.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.RegionId);
        return _context.Regions.AsNoTracking()
            .Where(r => granted.Contains(r.Id) || (r.ParentId != null && granted.Contains(r.ParentId.Value)))
            .Select(r => r.Id);
    }

    /// <summary>Towers reachable through an active mission or maintenance ticket.</summary>
    private IQueryable<Guid> AssignedTowerIds(Guid userId)
    {
        var activeMissionIds = _context.Missions.AsNoTracking()
            .Where(m => m.Status != MissionStatus.Completed && m.Status != MissionStatus.Cancelled
                && (m.ManagerId == userId || m.Assignments.Any(a => a.UserId == userId && a.Status == MissionAssignmentStatus.Active)))
            .Select(m => m.Id);
        var missionTowerIds = _context.MissionTargets.AsNoTracking()
            .Where(t => activeMissionIds.Contains(t.MissionId))
            .Select(t => t.TowerId);
        var ticketTowerIds = _context.MaintenanceTickets.AsNoTracking()
            .Where(t => (t.TechnicianId == userId || t.ManagerId == userId)
                && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled
                && t.Anomaly!.TowerId != null)
            .Select(t => t.Anomaly!.TowerId!.Value);
        return missionTowerIds.Union(ticketTowerIds);
    }

    public IQueryable<Tower> ApplyToTowers(IQueryable<Tower> towers)
    {
        if (IsSystemAdmin) return towers;
        if (IsAnonymous) return towers.Where(_ => false);
        var userId = _currentUser!.UserId;

        var regionIds = ScopedRegionIds(userId);
        var assignedTowerIds = AssignedTowerIds(userId);
        return towers.Where(tower => regionIds.Contains(tower.RegionId) || assignedTowerIds.Contains(tower.Id));
    }

    public IQueryable<Region> ApplyToRegions(IQueryable<Region> regions)
    {
        if (IsSystemAdmin) return regions;
        if (IsAnonymous) return regions.Where(_ => false);
        var userId = _currentUser!.UserId;

        var regionIds = ScopedRegionIds(userId);
        var assignedTowerIds = AssignedTowerIds(userId);
        var assignedRegionIds = _context.Towers.AsNoTracking()
            .Where(t => assignedTowerIds.Contains(t.Id))
            .Select(t => t.RegionId);
        return regions.Where(r => regionIds.Contains(r.Id) || assignedRegionIds.Contains(r.Id));
    }
}
