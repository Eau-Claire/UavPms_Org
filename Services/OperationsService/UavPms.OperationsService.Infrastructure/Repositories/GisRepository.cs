using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Gis.Infrastructure;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Authorization;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class GisRepository : IGisRepository
{
    private readonly ApplicationDbContext _context;
    private readonly GeographicAccessFilter _access;
    public GisRepository(ApplicationDbContext context, GeographicAccessFilter access)
    {
        _context = context;
        _access = access;
    }

    public async Task<GisInfrastructureResponse> GetInfrastructureAsync(GisInfrastructureQuery request, CancellationToken ct)
    {
        var towers = _access.ApplyToTowers(_context.Towers.AsNoTracking()).Where(x => x.Geom != null);
        if (request.AdministrativeAreaId.HasValue)
        {
            var regionId = request.AdministrativeAreaId.Value;
            if (!await _access.ApplyToRegions(_context.Regions).AsNoTracking().AnyAsync(x => x.Id == regionId, ct))
                throw new NotFoundException("AdministrativeArea", regionId);
            towers = towers.Where(x => x.RegionId == regionId || x.Region!.ParentId == regionId);
        }

        var towerDtos = await towers
            .Select(x => new GisTowerDto(x.Id, x.TowerCode, x.RegionId, x.PoleType, x.Address, x.Geom!.Coordinate.Y, x.Geom.Coordinate.X))
            .ToListAsync(ct);

        var towerIds = towers.Select(t => t.Id);
        var anomalies = await _context.DetectedAnomalies.AsNoTracking()
            .Where(a => a.TowerId != null && towerIds.Contains(a.TowerId.Value) && a.SupersededById == null)
            .Select(a => new GisAnomalyDto(a.Id, a.Id, a.TowerId, a.Tower!.TowerCode,
                a.Category!.CategoryName, a.Category.SeverityWeight * 5, a.Tower.Geom!.Coordinate.Y, a.Tower.Geom.Coordinate.X,
                a.ValidationStatus, a.ConfidenceScore, a.ImageUrl, a.CreatedAt)).ToListAsync(ct);
        var alerts = await _context.EmergencyAlerts.AsNoTracking()
            .Where(a => a.Status == "Active" && a.Anomaly!.TowerId != null && towerIds.Contains(a.Anomaly.TowerId.Value))
            .Select(a => new GisAlertDto(a.Id, a.AnomalyId, a.Anomaly!.TowerId, a.Anomaly.Tower!.TowerCode,
                a.Anomaly.Tower.Geom!.Coordinate.Y, a.Anomaly.Tower.Geom.Coordinate.X, a.Status, a.Priority,
                a.Anomaly.Category!.CategoryName, a.TriggeredAt)).ToListAsync(ct);
        return new(towerDtos) { Anomalies = anomalies, Alerts = alerts };
    }
}
