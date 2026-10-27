using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface ITowerRepository : IGenericRepository<Tower>
{
    Task<IReadOnlyList<Tower>> GetTowersInBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng);
    Task<IReadOnlyList<Tower>> GetTowersWithinDistanceAsync(double latitude, double longitude, double distanceInMeters);

    Task<(IReadOnlyList<Tower> Items, int TotalCount)> GetTowersPagedAsync(
        int page,
        int pageSize,
        Guid? regionId,
        string? search);

    /// <summary>Tower (with region) if it exists and the current user may see it.</summary>
    Task<Tower?> GetAccessibleByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Accessible towers whose point lies inside or on the polygon.</summary>
    Task<IReadOnlyList<Tower>> GetTowersIntersectingAsync(Polygon polygon, Guid? regionId, CancellationToken cancellationToken = default);
}
