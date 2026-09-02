using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Gis.Infrastructure;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class GisRepository : IGisRepository
{
    private readonly ApplicationDbContext _context;
    public GisRepository(ApplicationDbContext context) => _context = context;

    public async Task<GisInfrastructureResponse> GetInfrastructureAsync(GisInfrastructureQuery request, CancellationToken ct)
    {
        NetTopologySuite.Geometries.Geometry? area = null;
        if (request.AdministrativeAreaId.HasValue)
        {
            area = await _context.Regions.Where(x => x.Id == request.AdministrativeAreaId).Select(x => x.Geom).SingleOrDefaultAsync(ct);
            if (area is null) throw new NotFoundException("AdministrativeArea", request.AdministrativeAreaId.Value);
        }
        if (request.ManagementUnitId.HasValue && !await _context.ManagementUnits.AnyAsync(x => x.Id == request.ManagementUnitId, ct))
            throw new NotFoundException("ManagementUnit", "MANAGEMENT_UNIT_NOT_FOUND");
        if (request.PowerLineId.HasValue && !await _context.TransmissionLines.AnyAsync(x => x.Id == request.PowerLineId, ct))
            throw new NotFoundException("PowerLine", "POWER_LINE_NOT_FOUND");

        var lines = _context.TransmissionLines.AsNoTracking().AsQueryable();
        var segments = _context.LineSegments.AsNoTracking().AsQueryable();
        var assets = _context.Assets.AsNoTracking().Where(x => x.Location != null);
        if (request.ManagementUnitId.HasValue) { lines = lines.Where(x => x.ManagementUnitId == request.ManagementUnitId); assets = assets.Where(x => x.ManagementUnitId == request.ManagementUnitId); }
        if (request.PowerLineId.HasValue) { lines = lines.Where(x => x.Id == request.PowerLineId); segments = segments.Where(x => x.PowerLineId == request.PowerLineId); assets = assets.Where(x => x.PowerLineId == request.PowerLineId); }
        if (!string.IsNullOrWhiteSpace(request.VoltageLevel)) lines = lines.Where(x => x.VoltageLevel == request.VoltageLevel);
        if (!string.IsNullOrWhiteSpace(request.AssetType)) assets = assets.Where(x => x.AssetType == request.AssetType);
        if (!string.IsNullOrWhiteSpace(request.Status)) { lines = lines.Where(x => x.Status == request.Status); segments = segments.Where(x => x.Status == request.Status); assets = assets.Where(x => x.Status == request.Status); }
        if (area is not null) { lines = lines.Where(x => x.Geom != null && x.Geom.Intersects(area)); assets = assets.Where(x => x.Location!.Intersects(area)); }
        var lineIds = lines.Select(x => x.Id);
        segments = segments.Where(x => lineIds.Contains(x.PowerLineId));
        if (!string.IsNullOrWhiteSpace(request.VoltageLevel)) assets = assets.Where(x => x.PowerLine != null && x.PowerLine.VoltageLevel == request.VoltageLevel);

        var lineDtos = await lines.Select(x => new GisPowerLineDto(x.Id, x.Code, x.LineName, x.VoltageLevel, x.ManagementUnitId, x.Status, x.Geom == null ? null : x.Geom.AsText())).ToListAsync(ct);
        var segmentDtos = await segments.Select(x => new GisLineSegmentDto(x.Id, x.PowerLineId, x.FromAssetId, x.ToAssetId, x.Sequence, x.Status, x.Geometry == null ? null : x.Geometry.AsText())).ToListAsync(ct);
        var assetDtos = await assets.Select(x => new GisAssetDto(x.Id, x.AssetCode, x.AssetType, x.Status, x.ManagementUnitId, x.PowerLineId, x.Location!.Y, x.Location.X)).ToListAsync(ct);
        return new(lineDtos, segmentDtos, assetDtos);
    }
}
