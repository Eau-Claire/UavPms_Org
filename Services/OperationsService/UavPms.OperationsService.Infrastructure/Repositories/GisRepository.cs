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
        NetTopologySuite.Geometries.Geometry? area = null;
        if (request.AdministrativeAreaId.HasValue)
        {
            var region = await _access.ApplyToRegions(_context.Regions).AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.AdministrativeAreaId, ct);
            if (region is null) throw new NotFoundException("AdministrativeArea", request.AdministrativeAreaId.Value);
            area = region.Geom;
        }
        if (request.ManagementUnitId.HasValue && !await _access.ApplyToManagementUnits(_context.ManagementUnits).AnyAsync(x => x.Id == request.ManagementUnitId, ct))
            throw new NotFoundException("ManagementUnit", "MANAGEMENT_UNIT_NOT_FOUND");
        if (request.PowerLineId.HasValue && !await _access.ApplyToLines(_context.TransmissionLines).AnyAsync(x => x.Id == request.PowerLineId, ct))
            throw new NotFoundException("PowerLine", "POWER_LINE_NOT_FOUND");

        var lines = _access.ApplyToLines(_context.TransmissionLines.AsNoTracking());
        var segments = _context.LineSegments.AsNoTracking().AsQueryable();
        var assets = _access.ApplyToAssets(_context.Assets.AsNoTracking()).Where(x => x.Location != null);
        if (request.ManagementUnitId.HasValue) { lines = lines.Where(x => x.ManagementUnitId == request.ManagementUnitId); assets = assets.Where(x => x.ManagementUnitId == request.ManagementUnitId); }
        if (request.PowerLineId.HasValue) { lines = lines.Where(x => x.Id == request.PowerLineId); segments = segments.Where(x => x.PowerLineId == request.PowerLineId); assets = assets.Where(x => x.PowerLineId == request.PowerLineId); }
        if (!string.IsNullOrWhiteSpace(request.VoltageLevel)) lines = lines.Where(x => x.VoltageLevel == request.VoltageLevel);
        if (!string.IsNullOrWhiteSpace(request.AssetType)) assets = assets.Where(x => x.AssetType == request.AssetType);
        if (!string.IsNullOrWhiteSpace(request.Status)) { lines = lines.Where(x => x.Status == request.Status); segments = segments.Where(x => x.Status == request.Status); assets = assets.Where(x => x.Status == request.Status); }
        if (request.AdministrativeAreaId.HasValue)
        {
            // Organization regions need no invented polygons: their domain relationships are sufficient.
            lines = lines.Where(x => x.Substation!.RegionAssetId == request.AdministrativeAreaId
                || (area != null && x.Geom != null && x.Geom.Intersects(area)));
            assets = assets.Where(x => x.Tower!.TransmissionLine!.Substation!.RegionAssetId == request.AdministrativeAreaId
                || (area != null && x.Location!.Intersects(area)));
        }
        if (!string.IsNullOrWhiteSpace(request.VoltageLevel)) assets = assets.Where(x => x.PowerLine != null && x.PowerLine.VoltageLevel == request.VoltageLevel);
        var allowedAssetIds = assets.Select(x => x.Id);
        segments = segments.Where(s => allowedAssetIds.Contains(s.FromAssetId) && allowedAssetIds.Contains(s.ToAssetId));
        var lineIds = lines.Select(x => x.Id);
        segments = segments.Where(x => lineIds.Contains(x.PowerLineId));


        var lineDtos = await lines.Select(x => new GisPowerLineDto(x.Id, x.Code, x.LineName, x.VoltageLevel, x.ManagementUnitId, x.Status, x.Geom == null ? null : x.Geom.AsText())).ToListAsync(ct);
        var segmentDtos = await segments.Select(x => new GisLineSegmentDto(x.Id, x.PowerLineId, x.FromAssetId, x.ToAssetId, x.Sequence, x.Status, x.Geometry == null ? null : x.Geometry.AsText())).ToListAsync(ct);
        var assetDtos = await assets.Select(x => new GisAssetDto(x.Id, x.AssetCode, x.AssetType, x.Status, x.ManagementUnitId, x.PowerLineId, x.Location!.Y, x.Location.X)).ToListAsync(ct);
        var assetIds = assets.Select(a => a.Id);
        var anomalies = await _context.DetectedAnomalies.AsNoTracking()
            .Where(a => a.AssetId != null && assetIds.Contains(a.AssetId.Value))
            .Select(a => new GisAnomalyDto(a.Id, a.Id, a.Asset!.AssetCode, a.Asset.Tower!.TowerCode,
                a.Category!.CategoryName, a.Category.SeverityWeight * 5, a.Asset.Location!.Y, a.Asset.Location.X,
                a.ValidationStatus, a.ConfidenceScore, a.ImageUrl, a.CreatedAt)).ToListAsync(ct);
        var alerts = await _context.EmergencyAlerts.AsNoTracking()
            .Where(a => a.Status == "Active" && a.AssetId != null && assetIds.Contains(a.AssetId.Value))
            .Select(a => new GisAlertDto(a.Id, a.AnomalyId, a.Asset!.AssetCode, a.Asset.Tower!.TowerCode,
                a.Asset.Location!.Y, a.Asset.Location.X, a.Status, a.Priority, a.Anomaly!.Category!.CategoryName, a.TriggeredAt)).ToListAsync(ct);
        return new(lineDtos, segmentDtos, assetDtos) { Anomalies = anomalies, Alerts = alerts };
    }
}
