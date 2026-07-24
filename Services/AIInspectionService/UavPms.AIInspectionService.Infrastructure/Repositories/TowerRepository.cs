using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Infrastructure.Persistence;

namespace UavPms.AIInspectionService.Infrastructure.Repositories;

public class TowerRepository : GenericRepository<Tower>, ITowerRepository
{
    public TowerRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Tower>> GetTowersInBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng)
    {
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var envelope = new Envelope(minLng, maxLng, minLat, maxLat);
        var box = geometryFactory.ToGeometry(envelope);

        return await _context.Towers
            .Where(t => t.Geom != null && t.Geom.Within(box))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Tower>> GetTowersWithinDistanceAsync(double latitude, double longitude, double distanceInMeters)
    {
        return await _context.Towers
            .FromSqlInterpolated($"SELECT * FROM \"Towers\" WHERE \"IsDeleted\" = false AND ST_DWithin(\"Geom\"::geography, ST_SetSRID(ST_Point({longitude}, {latitude}), 4326)::geography, {distanceInMeters})")
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<Tower> Items, int TotalCount)> GetTowersPagedAsync(
        int page,
        int pageSize,
        Guid? lineAssetId)
    {
        var query = _context.Towers.Where(t => !t.IsDeleted);

        if (lineAssetId.HasValue)
        {
            query = query.Where(t => t.LineAssetId == lineAssetId.Value);
        }

        int totalCount = await query.CountAsync();
        
        var items = await query
            .OrderBy(t => t.TowerCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (items, totalCount);
    }
}