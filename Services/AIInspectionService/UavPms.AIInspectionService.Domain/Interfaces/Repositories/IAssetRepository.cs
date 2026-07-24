using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Domain.Interfaces.Repositories;

public interface IAssetRepository : IGenericRepository<Asset>
{
    Task<IReadOnlyList<Asset>> GetAssetsInBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng);
    Task<IReadOnlyList<Asset>> GetAssetsWithinDistanceAsync(double latitude, double longitude, double distanceInMeters);

    Task<(IReadOnlyList<Asset> Items, int TotalCount)> GetAssetsPagedAsync(
        int page,
        int pageSize,
        Guid? towerId,
        string? assetType,
        string? status
    );
    Task<Asset?> GetAssetWithDetailsAsync(Guid id);
}