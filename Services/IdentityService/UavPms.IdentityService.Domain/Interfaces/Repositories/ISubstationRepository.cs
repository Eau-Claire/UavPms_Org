using UavPms.IdentityService.Domain.Entities;

namespace UavPms.IdentityService.Domain.Interfaces.Repositories;

public interface ISubstationRepository : IGenericRepository<Substation>
{
Task<(IReadOnlyList<Substation> Items, int TotalCount)> GetSubstationsPagedAsync(
        int page,
        int pageSize,
        Guid? regionAssetId,
        string? searchTerm
    );
}