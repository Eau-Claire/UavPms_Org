using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Domain.Interfaces.Repositories;

public interface ITransmissionLineRepository : IGenericRepository<TransmissionLine>
{
    Task<(IReadOnlyList<TransmissionLine> Items, int TotalCount)> GetTransmissionLinesPagedAsync(
        int page,
        int pageSize,
        Guid? substationAssetId,
        string? searchTerm
    );
}