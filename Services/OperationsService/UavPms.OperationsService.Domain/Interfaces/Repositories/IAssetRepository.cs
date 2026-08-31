using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IAssetRepository : IGenericRepository<Asset>
{
    Task<IReadOnlyList<Asset>> GetAssetsInBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng);
    Task<IReadOnlyList<Asset>> GetAssetsWithinDistanceAsync(double latitude, double longitude, double distanceInMeters);

    Task<(IReadOnlyList<Asset> Items, int TotalCount)> GetAssetsPagedAsync(
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
        IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken);
    Task<AssetHealthSummary> GetAssetHealthSummaryAsync(CancellationToken cancellationToken);
    Task<Asset?> GetAssetWithDetailsAsync(Guid id);
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
