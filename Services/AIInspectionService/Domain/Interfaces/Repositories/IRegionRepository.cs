using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Domain.Interfaces.Repositories;

public interface IRegionRepository : IGenericRepository<Region>
{
    Task<(IReadOnlyList<Region> Items, int TotalCount)> GetRegionsPagedAsync(
        int page,
        int pageSize,
        string? searchTerm
    );
}