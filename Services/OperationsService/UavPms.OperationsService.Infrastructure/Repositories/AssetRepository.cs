using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class AssetRepository : GenericRepository<Asset>, IAssetRepository
{
    public AssetRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Asset>> GetAssetsInBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng)
    {
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var envelope = new Envelope(minLng, maxLng, minLat, maxLat);
        var box = geometryFactory.ToGeometry(envelope);

        return await _context.Assets
            .Include(a => a.Tower)
            .Where(a => a.Tower != null && a.Tower.Geom != null && a.Tower.Geom.Within(box))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Asset>> GetAssetsWithinDistanceAsync(double latitude, double longitude, double distanceInMeters)
    {
        return await _context.Assets
            .FromSqlInterpolated($@"
                SELECT a.* 
                FROM ""Assets"" a 
                JOIN ""Towers"" t ON a.""TowerId"" = t.""Id"" 
                WHERE a.""IsDeleted"" = false 
                  AND t.""IsDeleted"" = false 
                  AND ST_DWithin(t.""Geom""::geography, ST_SetSRID(ST_Point({longitude}, {latitude}), 4326)::geography, {distanceInMeters})")
            .Include(a => a.Tower)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<Asset> Items, int TotalCount)> GetAssetsPagedAsync(int page, int pageSize, Guid? towerId, string? assetType, string? status)
    {
        var query = _context.Assets.Where(a => !a.IsDeleted);

        if (towerId.HasValue)
        {
            query = query.Where(a => a.TowerId == towerId.Value);
        }

        if (!string.IsNullOrEmpty(assetType))
        {
            query = query.Where(a => a.AssetType == assetType);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(a => a.Status == status);
        }

        int totalCount = await query.CountAsync();
        
        var items = await query
            .OrderBy(a => a.AssetCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (items, totalCount);
    }

    public async Task<Asset?> GetAssetWithDetailsAsync(Guid id)
    {
        return await _context.Assets
            .Include(a => a.Tower)
            .Include(a => a.DetectedAnomalies)
                .ThenInclude(da => da.Category)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }
}