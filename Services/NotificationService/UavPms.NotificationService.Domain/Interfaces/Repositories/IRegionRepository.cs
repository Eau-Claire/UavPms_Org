using UavPms.NotificationService.Domain.Entities;

namespace UavPms.NotificationService.Domain.Interfaces.Repositories;

public interface IRegionRepository : IGenericRepository<Region>
{
    Task<(IReadOnlyList<Region> Items, int TotalCount)> GetRegionsPagedAsync(
        int page,
        int pageSize,
        string? searchTerm
    );
}