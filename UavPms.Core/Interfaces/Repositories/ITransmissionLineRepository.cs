using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Repositories;

public interface ITransmissionLineRepository : IGenericRepository<TransmissionLine>
{
    Task<(IReadOnlyList<TransmissionLine> Items, int TotalCount)> GetTransmissionLinesPagedAsync(
        int page,
        int pageSize,
        Guid? substationAssetId,
        string? searchTerm
    );
}