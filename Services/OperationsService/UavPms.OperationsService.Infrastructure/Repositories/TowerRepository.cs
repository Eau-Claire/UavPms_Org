using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Authorization;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class TowerRepository : GenericRepository<Tower>, ITowerRepository
{
    private readonly GeographicAccessFilter _access;

    public TowerRepository(ApplicationDbContext context, GeographicAccessFilter access) : base(context)
    {
        _access = access;
    }

    public async Task<IReadOnlyList<Tower>> GetTowersInBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng)
    {
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var envelope = new Envelope(minLng, maxLng, minLat, maxLat);
        var box = geometryFactory.ToGeometry(envelope);

        return await _access.ApplyToTowers(_context.Towers)
            .Where(t => t.Geom != null && t.Geom.Within(box))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Tower>> GetTowersWithinDistanceAsync(double latitude, double longitude, double distanceInMeters)
    {
        var accessibleTowerIds = _access.ApplyToTowers(_context.Towers.AsNoTracking()).Select(t => t.Id);
        return await _context.Towers
            .FromSqlInterpolated($"SELECT * FROM \"Towers\" WHERE \"IsDeleted\" = false AND ST_DWithin(\"Geom\"::geography, ST_SetSRID(ST_Point({longitude}, {latitude}), 4326)::geography, {distanceInMeters})")
            .Where(t => accessibleTowerIds.Contains(t.Id))
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<Tower> Items, int TotalCount)> GetTowersPagedAsync(
        int page,
        int pageSize,
        Guid? regionId,
        string? search)
    {
        var query = _access.ApplyToTowers(_context.Towers.Where(t => !t.IsDeleted));

        if (regionId.HasValue)
        {
            query = query.Where(t => t.RegionId == regionId.Value || t.Region!.ParentId == regionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(t => t.TowerCode.ToLower().Contains(term)
                || (t.Address != null && t.Address.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(t => t.Region)
            .OrderBy(t => t.TowerCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Tower?> GetAccessibleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _access.ApplyToTowers(_context.Towers.AsNoTracking())
            .Include(t => t.Region)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Tower>> GetTowersIntersectingAsync(Polygon polygon, Guid? regionId, CancellationToken cancellationToken = default)
    {
        // Boundary points are selectable, hence Intersects rather than Within.
        var query = _access.ApplyToTowers(_context.Towers.AsNoTracking())
            .Where(t => t.Geom != null && t.Geom.Intersects(polygon));
        if (regionId.HasValue)
            query = query.Where(t => t.RegionId == regionId.Value);
        return await query.OrderBy(t => t.TowerCode).ToListAsync(cancellationToken);
    }
}
