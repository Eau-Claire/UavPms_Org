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
                FROM ""AssetComponents"" a
                JOIN ""Towers"" t ON a.""TowerId"" = t.""Id"" 
                WHERE a.""IsDeleted"" = false 
                  AND t.""IsDeleted"" = false 
                  AND ST_DWithin(t.""Geom""::geography, ST_SetSRID(ST_Point({longitude}, {latitude}), 4326)::geography, {distanceInMeters})")
            .Include(a => a.Tower)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<Asset> Items, int TotalCount)> GetAssetsPagedAsync(
        int page,
        int pageSize,
        Guid? towerId,
        string? assetType,
        string? status,
        IReadOnlyList<string>? riskLevels = null,
        double? minHealthScore = null,
        double? maxHealthScore = null,
        Guid? regionId = null,
        Guid? lineId = null,
        string? sortBy = null,
        string? sortOrder = null)
    {
        var query = _context.Assets
            .Where(a => !a.IsDeleted)
            .Include(a => a.Tower)
                .ThenInclude(t => t!.TransmissionLine)
                    .ThenInclude(l => l!.Substation)
                        .ThenInclude(s => s!.Region)
            .AsQueryable();

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

        if (riskLevels is { Count: > 0 })
        {
            var normalizedRiskLevels = riskLevels
                .Where(riskLevel => !string.IsNullOrWhiteSpace(riskLevel))
                .Select(riskLevel => riskLevel.Trim().ToLower())
                .ToList();

            if (normalizedRiskLevels.Count > 0)
            {
                query = query.Where(a => normalizedRiskLevels.Contains(a.RiskLevel.ToLower()));
            }
        }

        if (minHealthScore.HasValue)
        {
            query = query.Where(a => a.CurrentHealthScore >= minHealthScore.Value);
        }

        if (maxHealthScore.HasValue)
        {
            query = query.Where(a => a.CurrentHealthScore <= maxHealthScore.Value);
        }

        if (lineId.HasValue)
        {
            query = query.Where(a => a.Tower != null && a.Tower.LineAssetId == lineId.Value);
        }

        if (regionId.HasValue)
        {
            query = query.Where(a =>
                a.Tower != null &&
                a.Tower.TransmissionLine != null &&
                a.Tower.TransmissionLine.Substation != null &&
                a.Tower.TransmissionLine.Substation.RegionAssetId == regionId.Value);
        }

        int totalCount = await query.CountAsync();
        
        var items = await query
            .ApplyAssetSort(sortBy, sortOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (items, totalCount);
    }

    public async Task<Asset?> GetAssetWithDetailsAsync(Guid id)
    {
        return await _context.Assets
            .Include(a => a.Tower)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }

    public async Task<IReadOnlyList<Asset>> GetAssetsByIdsAsync(
        IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0)
        {
            return Array.Empty<Asset>();
        }

        return await _context.Assets
            .Include(a => a.Tower)
            .Include(a => a.PowerLine)
            .Where(a => assetIds.Contains(a.Id) && !a.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SpatialAssetMatch>> GetAssetsIntersectingAsync(
        Polygon polygon,
        Guid? managementUnitId,
        Guid? powerLineId,
        string? assetType,
        CancellationToken cancellationToken)
    {
        var query = _context.Assets
            .Where(a => !a.IsDeleted
                        && (a.Status == "Active" || a.Status == "Operational")
                        && a.Location != null
                        // Boundary points are selectable, hence Intersects rather than Within.
                        && a.Location.Intersects(polygon));
        if (managementUnitId.HasValue) query = query.Where(a => a.ManagementUnitId == managementUnitId);
        if (powerLineId.HasValue) query = query.Where(a => a.PowerLineId == powerLineId);
        if (!string.IsNullOrWhiteSpace(assetType)) query = query.Where(a => a.AssetType == assetType);

        return await query
            .Select(a => new SpatialAssetMatch
            {
                Id = a.Id,
                AssetCode = a.AssetCode,
                Name = a.AssetType,
                AssetType = a.AssetType,
                Latitude = a.Location!.Y,
                Longitude = a.Location.X,
                Status = a.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetConfirmedDefectCountsAsync(
        IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _context.DetectedAnomalies
            .Where(d => d.AssetId.HasValue
                        && assetIds.Contains(d.AssetId.Value)
                        && d.ValidationStatus == "Confirmed")
            .GroupBy(d => d.AssetId!.Value)
            .Select(g => new { AssetId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.AssetId, g => g.Count, cancellationToken);
    }

    public async Task<AssetHealthSummary> GetAssetHealthSummaryAsync(CancellationToken cancellationToken)
    {
        var assets = await _context.Assets
            .Where(a => !a.IsDeleted)
            .Include(a => a.Tower)
            .ToListAsync(cancellationToken);

        var defectCounts = await GetConfirmedDefectCountsAsync(
            assets.Select(a => a.Id).ToList(),
            cancellationToken);

        var totalAssets = assets.Count;
        var averageHealthScore = totalAssets == 0
            ? 0
            : Math.Round(assets.Average(a => a.CurrentHealthScore), 2);

        var criticalAssets = assets
            .Where(a => IsRiskLevel(a.RiskLevel, "Critical Risk"))
            .OrderBy(a => a.CurrentHealthScore)
            .ThenBy(a => a.AssetCode)
            .Take(10)
            .Select(a => new AssetHealthSummaryItem(
                a.Id,
                a.AssetCode,
                a.AssetType,
                a.CurrentHealthScore,
                a.RiskLevel,
                defectCounts.GetValueOrDefault(a.Id),
                a.Tower?.TowerCode))
            .ToList();

        return new AssetHealthSummary(
            totalAssets,
            averageHealthScore,
            assets.Count(a => IsRiskLevel(a.RiskLevel, "Critical Risk")),
            assets.Count(a => IsRiskLevel(a.RiskLevel, "High Risk")),
            assets.Count(a => IsRiskLevel(a.RiskLevel, "Medium Risk")),
            assets.Count(a => IsRiskLevel(a.RiskLevel, "Low Risk")),
            criticalAssets);
    }

    private static bool IsRiskLevel(string value, string expected)
        => string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
}

internal static class AssetSortingExtensions
{
    public static IQueryable<Asset> ApplyAssetSort(
        this IQueryable<Asset> query,
        string? sortBy,
        string? sortOrder)
    {
        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "healthscore" => descending
                ? query.OrderByDescending(a => a.CurrentHealthScore).ThenBy(a => a.AssetCode)
                : query.OrderBy(a => a.CurrentHealthScore).ThenBy(a => a.AssetCode),
            "risklevel" => query.OrderBy(a =>
                    a.RiskLevel == "Critical Risk" ? 0 :
                    a.RiskLevel == "High Risk" ? 1 :
                    a.RiskLevel == "Medium Risk" ? 2 :
                    a.RiskLevel == "Low Risk" ? 3 : 4)
                .ThenBy(a => a.CurrentHealthScore)
                .ThenBy(a => a.AssetCode),
            "lastinspectedat" => descending
                ? query.OrderByDescending(a => a.LastInspectedAt).ThenBy(a => a.AssetCode)
                : query.OrderBy(a => a.LastInspectedAt).ThenBy(a => a.AssetCode),
            "assetcode" => descending
                ? query.OrderByDescending(a => a.AssetCode)
                : query.OrderBy(a => a.AssetCode),
            _ => query.OrderBy(a =>
                    a.RiskLevel == "Critical Risk" ? 0 :
                    a.RiskLevel == "High Risk" ? 1 :
                    a.RiskLevel == "Medium Risk" ? 2 :
                    a.RiskLevel == "Low Risk" ? 3 : 4)
                .ThenBy(a => a.CurrentHealthScore)
                .ThenBy(a => a.AssetCode)
        };
    }
}
