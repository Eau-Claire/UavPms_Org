using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Repositories;

public interface IRegionRepository : IGenericRepository<Region>
{
    Task<(IReadOnlyList<Region> Items, int TotalCount)> GetRegionsPagedAsync(
        int page,
        int pageSize,
        string? searchTerm
    );
}