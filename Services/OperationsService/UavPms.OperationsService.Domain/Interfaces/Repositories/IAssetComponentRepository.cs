using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IAssetComponentRepository : IGenericRepository<AssetComponent>
{
    Task<IReadOnlyList<SpatialAssetMatch>> GetAssetComponentsIntersectingAsync(Polygon polygon, CancellationToken cancellationToken);
    Task<IReadOnlyList<SpatialAssetMatch>> GetAssetComponentsInBoundingBoxAsync(
        double minLat, double minLng, double maxLat, double maxLng, CancellationToken cancellationToken);
    Task<IReadOnlyList<SpatialAssetMatch>> GetAssetComponentsWithinDistanceAsync(
        double latitude, double longitude, double distanceInMeters, CancellationToken cancellationToken);

    Task<(IReadOnlyList<AssetComponent> Items, int TotalCount)> GetAssetComponentsPagedAsync(
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
        string? sortOrder = null
    );
    Task<IReadOnlyDictionary<Guid, int>> GetConfirmedDefectCountsAsync(
        IReadOnlyCollection<Guid> componentIds,
        CancellationToken cancellationToken);
    Task<AssetHealthSummary> GetAssetHealthSummaryAsync(CancellationToken cancellationToken);
    Task<AssetComponent?> GetAssetWithDetailsAsync(Guid id);
}

public sealed record AssetHealthSummary(
    int TotalAssets,
    double AverageHealthScore,
    int CriticalRiskCount,
    int HighRiskCount,
    int MediumRiskCount,
    int LowRiskCount,
    IReadOnlyList<AssetHealthSummaryItem> CriticalAssets);

public sealed record AssetHealthSummaryItem(
    Guid Id,
    string AssetCode,
    string AssetType,
    double CurrentHealthScore,
    string RiskLevel,
    int DefectCount,
    string? TowerCode);
