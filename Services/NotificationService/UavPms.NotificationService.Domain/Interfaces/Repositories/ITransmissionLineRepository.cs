using UavPms.NotificationService.Domain.Entities;

namespace UavPms.NotificationService.Domain.Interfaces.Repositories;

public interface ITransmissionLineRepository : IGenericRepository<TransmissionLine>
{
    Task<(IReadOnlyList<TransmissionLine> Items, int TotalCount)> GetTransmissionLinesPagedAsync(
        int page,
        int pageSize,
        Guid? substationAssetId,
        string? searchTerm
    );
}