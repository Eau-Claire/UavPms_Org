using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class AssetComponentRepository : GenericRepository<AssetComponent>, IAssetComponentRepository
{
    public AssetComponentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<SpatialAssetMatch>> GetAssetComponentsIntersectingAsync(
        Polygon polygon,
        CancellationToken cancellationToken)
    {
        return await SelectableAssets()
            .Where(a => a.Tower!.Geom!.Intersects(polygon))
            .Select(a => new SpatialAssetMatch
            {
                Id = a.Id,
                Code = a.ComponentCode,
                Name = a.ComponentType,
                Latitude = a.Tower!.Geom!.Y,
                Longitude = a.Tower.Geom.X,
                Status = a.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SpatialAssetMatch>> GetAssetComponentsInBoundingBoxAsync(
        double minLat,
        double minLng,
        double maxLat,
        double maxLng,
        CancellationToken cancellationToken)
    {
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var envelope = new Envelope(minLng, maxLng, minLat, maxLat);
        var box = geometryFactory.ToGeometry(envelope);

        return await SelectableAssets()
            .Where(a => a.Tower!.Geom!.Intersects(box))
            .Select(a => new SpatialAssetMatch
            {
                Id = a.Id,
                Code = a.ComponentCode,
                Name = a.ComponentType,
                Latitude = a.Tower!.Geom!.Y,
                Longitude = a.Tower.Geom.X,
                Status = a.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SpatialAssetMatch>> GetAssetComponentsWithinDistanceAsync(
        double latitude,
        double longitude,
        double distanceInMeters,
        CancellationToken cancellationToken)
    {
        return await _context.Database.SqlQuery<SpatialAssetMatch>($@"
                SELECT a.""Id"" AS ""Id"",
                       a.""ComponentCode"" AS ""Code"",
                       a.""ComponentType"" AS ""Name"",
                       ST_Y(t.""Location"") AS ""Latitude"",
                       ST_X(t.""Location"") AS ""Longitude"",
                       a.""Status"" AS ""Status"",
                       ST_Distance(
                           t.""Location""::geography,
                           ST_SetSRID(ST_Point({longitude}, {latitude}), 4326)::geography) AS ""DistanceMeters""
                FROM ""AssetComponents"" a
                JOIN ""Towers"" t ON a.""TowerId"" = t.""Id""
                WHERE NOT a.""IsDeleted""
                  AND NOT t.""IsDeleted""
                  AND a.""Status"" IN ('Active', 'Operational')
                  AND t.""Location"" IS NOT NULL
                  AND ST_DWithin(
                      t.""Location""::geography,
                      ST_SetSRID(ST_Point({longitude}, {latitude}), 4326)::geography,
                      {distanceInMeters})
                ORDER BY ""DistanceMeters""")
            .ToListAsync(cancellationToken);
    }

    private IQueryable<AssetComponent> SelectableAssets() => _context.AssetComponents
        .Where(a => (a.Status == "Active" || a.Status == "Operational")
                    && a.Tower != null
                    && a.Tower.Geom != null);

    public async Task<(IReadOnlyList<AssetComponent> Items, int TotalCount)> GetAssetComponentsPagedAsync(
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
        var query = AssetComponentDetailsQuery();

        if (towerId.HasValue)
        {
            query = query.Where(a => a.TowerId == towerId.Value);
        }

        if (!string.IsNullOrEmpty(assetType))
        {
            query = query.Where(a => a.ComponentType == assetType);
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

    public async Task<AssetHealthSummary> GetAssetHealthSummaryAsync(CancellationToken cancellationToken)
    {
        var assets = await AssetComponentDetailsQuery().ToListAsync(cancellationToken);
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
            .ThenBy(a => a.ComponentCode)
            .Take(10)
            .Select(a => new AssetHealthSummaryItem(
                a.Id,
                a.ComponentCode,
                a.ComponentType,
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

    public async Task<AssetComponent?> GetAssetWithDetailsAsync(Guid id)
    {
        return await AssetComponentDetailsQuery()
            .Include(a => a.DetectedAnomalies)
                .ThenInclude(da => da.Category)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }

    private IQueryable<AssetComponent> AssetComponentDetailsQuery() => _context.AssetComponents
        .Where(a => !a.IsDeleted)
        .Include(a => a.Tower)
            .ThenInclude(t => t!.TransmissionLine)
                .ThenInclude(l => l!.Substation)
                    .ThenInclude(s => s!.Region);

    public async Task<IReadOnlyDictionary<Guid, int>> GetConfirmedDefectCountsAsync(
        IReadOnlyCollection<Guid> componentIds,
        CancellationToken cancellationToken)
    {
        if (componentIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _context.DetectedAnomalies
            .Where(d => d.ComponentId.HasValue
                        && componentIds.Contains(d.ComponentId.Value)
                        && d.ValidationStatus == "Confirmed")
            .GroupBy(d => d.ComponentId!.Value)
            .Select(g => new { ComponentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ComponentId, g => g.Count, cancellationToken);
    }

    private static bool IsRiskLevel(string value, string expected)
        => string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
}

internal static class AssetComponentSortingExtensions
{
    public static IQueryable<AssetComponent> ApplyAssetSort(
        this IQueryable<AssetComponent> query,
        string? sortBy,
        string? sortOrder)
    {
        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "healthscore" => descending
                ? query.OrderByDescending(a => a.CurrentHealthScore).ThenBy(a => a.ComponentCode)
                : query.OrderBy(a => a.CurrentHealthScore).ThenBy(a => a.ComponentCode),
            "risklevel" => descending
                ? query.OrderBy(a =>
                        a.RiskLevel == "Critical Risk" ? 0 :
                        a.RiskLevel == "High Risk" ? 1 :
                        a.RiskLevel == "Medium Risk" ? 2 :
                        a.RiskLevel == "Low Risk" ? 3 : 4)
                    .ThenBy(a => a.CurrentHealthScore)
                : query.OrderBy(a =>
                        a.RiskLevel == "Critical Risk" ? 0 :
                        a.RiskLevel == "High Risk" ? 1 :
                        a.RiskLevel == "Medium Risk" ? 2 :
                        a.RiskLevel == "Low Risk" ? 3 : 4)
                    .ThenBy(a => a.CurrentHealthScore),
            "lastinspectedat" => descending
                ? query.OrderByDescending(a => a.LastInspectedAt).ThenBy(a => a.ComponentCode)
                : query.OrderBy(a => a.LastInspectedAt).ThenBy(a => a.ComponentCode),
            "assetcode" => descending
                ? query.OrderByDescending(a => a.ComponentCode)
                : query.OrderBy(a => a.ComponentCode),
            _ => query.OrderBy(a =>
                    a.RiskLevel == "Critical Risk" ? 0 :
                    a.RiskLevel == "High Risk" ? 1 :
                    a.RiskLevel == "Medium Risk" ? 2 :
                    a.RiskLevel == "Low Risk" ? 3 : 4)
                .ThenBy(a => a.CurrentHealthScore)
                .ThenBy(a => a.ComponentCode)
        };
    }
}
