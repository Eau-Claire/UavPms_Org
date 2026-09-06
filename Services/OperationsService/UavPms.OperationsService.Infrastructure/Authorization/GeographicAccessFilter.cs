using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Infrastructure.Authorization;

/// <summary>
/// Applies the single GIS access rule in SQL: default geographic scope OR an active
/// assignment.  It intentionally returns IQueryable so callers cannot accidentally
/// fetch the whole map and filter it in memory.
/// </summary>
public sealed class GeographicAccessFilter
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserServices _currentUser;

    public GeographicAccessFilter(ApplicationDbContext context, ICurrentUserServices currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public IQueryable<Asset> ApplyToAssets(IQueryable<Asset> assets)
    {
        if (_currentUser.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase)) return assets;
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty) return assets.Where(_ => false);

        var userId = _currentUser.UserId;
        var scopes = _context.UserGeographicScopes.AsNoTracking().Where(s => s.UserId == userId);
        var missionAssetIds = _context.MissionTargets.AsNoTracking()
            .Where(t => (t.Mission!.InspectorId == userId || t.Mission.ManagerId == userId)
                && t.Mission.Status != MissionStatus.Completed && t.Mission.Status != MissionStatus.Cancelled)
            .Select(t => t.AssetId);
        var ticketAssetIds = _context.MaintenanceTickets.AsNoTracking()
            .Where(t => (t.TechnicianId == userId || t.ManagerId == userId)
                && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
            .Select(t => t.AssetId);

        return assets.Where(asset =>
            missionAssetIds.Contains(asset.Id)
            || ticketAssetIds.Contains(asset.Id)
            || scopes.Any(scope =>
                (scope.ManagementUnitId != null && asset.ManagementUnitId == scope.ManagementUnitId)
                || (scope.TransmissionLineId != null && (asset.PowerLineId == scope.TransmissionLineId || asset.Tower!.LineAssetId == scope.TransmissionLineId))
                || (scope.SubstationId != null && asset.Tower!.TransmissionLine!.SubstationAssetId == scope.SubstationId)
                || (scope.RegionId != null && asset.Tower!.TransmissionLine!.Substation!.RegionAssetId == scope.RegionId)));
    }

    public IQueryable<TransmissionLine> ApplyToLines(IQueryable<TransmissionLine> lines)
    {
        if (_currentUser.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase)) return lines;
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty) return lines.Where(_ => false);

        var userId = _currentUser.UserId;
        var scopes = _context.UserGeographicScopes.AsNoTracking().Where(s => s.UserId == userId);
        var assignedAssetIds = ApplyToAssets(_context.Assets.AsNoTracking()).Select(a => a.Id);
        return lines.Where(line => _context.Assets.Any(a => assignedAssetIds.Contains(a.Id) && (a.PowerLineId == line.Id || a.Tower!.LineAssetId == line.Id))
            || scopes.Any(scope => (scope.ManagementUnitId != null && line.ManagementUnitId == scope.ManagementUnitId)
                || scope.TransmissionLineId == line.Id
                || scope.SubstationId == line.SubstationAssetId
                || (scope.RegionId != null && line.Substation!.RegionAssetId == scope.RegionId)));
    }

    public IQueryable<Tower> ApplyToTowers(IQueryable<Tower> towers)
    {
        var allowedLineIds = ApplyToLines(_context.TransmissionLines.AsNoTracking()).Select(line => line.Id);
        return towers.Where(tower => allowedLineIds.Contains(tower.LineAssetId));
    }
}
