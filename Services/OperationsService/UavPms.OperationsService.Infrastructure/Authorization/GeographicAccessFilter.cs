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
    private readonly ICurrentUserServices? _currentUser;

    public GeographicAccessFilter(ApplicationDbContext context, ICurrentUserServices? currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public IQueryable<Asset> ApplyToAssets(IQueryable<Asset> assets)
    {
        if (_currentUser?.IsAuthenticated == true && _currentUser.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase)) return assets;
        if (_currentUser is null || !_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty) return assets.Where(_ => false);

        var userId = _currentUser.UserId;
        var scopes = _context.UserGeographicScopes.AsNoTracking().Where(s => s.UserId == userId);
        var missionAssetIds = _context.MissionTargets.AsNoTracking()
            .Where(t => (t.Mission!.InspectorId == userId || t.Mission.ManagerId == userId)
                && t.Mission.Status != MissionStatus.Completed && t.Mission.Status != MissionStatus.Cancelled)
            .Select(t => t.AssetId);
        var missionLineIds = _context.MissionTargetLines.AsNoTracking()
            .Where(t => (t.Mission!.InspectorId == userId || t.Mission.ManagerId == userId)
                && t.Mission.Status != MissionStatus.Completed && t.Mission.Status != MissionStatus.Cancelled)
            .Select(t => t.LineAssetId);
        var ticketAssetIds = _context.MaintenanceTickets.AsNoTracking()
            .Where(t => (t.TechnicianId == userId || t.ManagerId == userId)
                && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
            .Select(t => t.AssetId);

        return assets.Where(asset =>
            missionAssetIds.Contains(asset.Id)
            || missionLineIds.Contains(asset.Tower!.LineAssetId)
            || (asset.PowerLineId != null && missionLineIds.Contains(asset.PowerLineId.Value))
            || ticketAssetIds.Contains(asset.Id)
            || scopes.Any(scope =>
                (scope.ManagementUnitId != null && (asset.ManagementUnitId == scope.ManagementUnitId || asset.Tower!.TransmissionLine!.ManagementUnitId == scope.ManagementUnitId))
                || (scope.TransmissionLineId != null && (asset.PowerLineId == scope.TransmissionLineId || asset.Tower!.LineAssetId == scope.TransmissionLineId))
                || (scope.SubstationId != null && asset.Tower!.TransmissionLine!.SubstationAssetId == scope.SubstationId)
                || (scope.RegionId != null && asset.Tower!.TransmissionLine!.Substation!.RegionAssetId == scope.RegionId)));
    }

    public IQueryable<TransmissionLine> ApplyToLines(IQueryable<TransmissionLine> lines)
    {
        if (_currentUser?.IsAuthenticated == true && _currentUser.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase)) return lines;
        if (_currentUser is null || !_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty) return lines.Where(_ => false);

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
        if (_currentUser?.IsAuthenticated == true && _currentUser.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase)) return towers;
        if (_currentUser is null || !_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty) return towers.Where(_ => false);
        var userId = _currentUser.UserId;
        var scopes = _context.UserGeographicScopes.Where(s => s.UserId == userId);
        var allowedTowerIds = ApplyToAssets(_context.Assets).Select(a => a.TowerId);
        return towers.Where(tower => allowedTowerIds.Contains(tower.Id)
            || scopes.Any(scope => scope.TransmissionLineId == tower.LineAssetId
                || scope.SubstationId == tower.TransmissionLine!.SubstationAssetId
                || (scope.RegionId != null && scope.RegionId == tower.TransmissionLine!.Substation!.RegionAssetId)
                || (scope.ManagementUnitId != null && scope.ManagementUnitId == tower.TransmissionLine!.ManagementUnitId)));
    }

    public IQueryable<Substation> ApplyToSubstations(IQueryable<Substation> substations)
    {
        if (_currentUser?.IsAuthenticated == true && _currentUser.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase)) return substations;
        var lineSubstations = ApplyToLines(_context.TransmissionLines).Select(l => l.SubstationAssetId);
        var userId = _currentUser?.IsAuthenticated == true ? _currentUser.UserId : Guid.Empty;
        return substations.Where(s => lineSubstations.Contains(s.Id) || _context.UserGeographicScopes.Any(scope => scope.UserId == userId && userId != Guid.Empty && (scope.SubstationId == s.Id || scope.RegionId == s.RegionAssetId)));
    }

    public IQueryable<Region> ApplyToRegions(IQueryable<Region> regions)
    {
        if (_currentUser?.IsAuthenticated == true && _currentUser.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase)) return regions;
        var regionIds = ApplyToSubstations(_context.Substations).Select(s => s.RegionAssetId);
        var userId = _currentUser?.IsAuthenticated == true ? _currentUser.UserId : Guid.Empty;
        return regions.Where(r => regionIds.Contains(r.Id) || _context.UserGeographicScopes.Any(scope => scope.UserId == userId && userId != Guid.Empty && scope.RegionId == r.Id));
    }
    public IQueryable<ManagementUnit> ApplyToManagementUnits(IQueryable<ManagementUnit> units)
    {
        if (_currentUser?.IsAuthenticated == true && _currentUser.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase)) return units;
        var unitIds = ApplyToLines(_context.TransmissionLines).Select(l => l.ManagementUnitId);
        var assetUnitIds = ApplyToAssets(_context.Assets).Select(a => a.ManagementUnitId);
        var userId = _currentUser?.IsAuthenticated == true ? _currentUser.UserId : Guid.Empty;
        return units.Where(u => unitIds.Contains(u.Id) || assetUnitIds.Contains(u.Id)
            || _context.UserGeographicScopes.Any(s => s.UserId == userId && userId != Guid.Empty && s.ManagementUnitId == u.Id));
    }

}
