using UavPms.IdentityService.Domain.Entities;

namespace UavPms.IdentityService.Domain.Interfaces.Repositories;

public interface ITowerRepository : IGenericRepository<Tower> 
{
    Task<IReadOnlyList<Tower>> GetTowersInBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng);
    Task<IReadOnlyList<Tower>> GetTowersWithinDistanceAsync(double latitude, double longitude, double distanceInMeters);
    Task<(IReadOnlyList<Tower> Items, int TotalCount)> GetTowersPagedAsync(
        int page, 
        int pageSize,
        Guid? lineAssetId
    );
}