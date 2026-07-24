using UavPms.IdentityService.Domain.Entities;

namespace UavPms.IdentityService.Domain.Interfaces.Repositories;

public interface ITransmissionLineRepository : IGenericRepository<TransmissionLine>
{
    Task<(IReadOnlyList<TransmissionLine> Items, int TotalCount)> GetTransmissionLinesPagedAsync(
        int page,
        int pageSize,
        Guid? substationAssetId,
        string? searchTerm
    );
}