using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IAnomalyRepository : IGenericRepository<DetectedAnomaly>
{
    Task<DetectedAnomaly?> GetByIdWithDetailAsync(Guid id);
    Task<IReadOnlyList<DetectedAnomaly>> GetAllWithDetailsAsync();
    Task<IReadOnlyList<DetectedAnomaly>> GetPendingWithDetailsAsync();
    /// <summary>Anomalies of a tower that are not superseded, newest first, filtered and paged in SQL.</summary>
    Task<(IReadOnlyList<DetectedAnomaly> Items, int TotalCount)> GetByTowerIdPagedAsync(
        Guid towerId, string? validationStatus, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default);
    /// <summary>Count of non-superseded anomalies of a tower per ValidationStatus.</summary>
    Task<IReadOnlyDictionary<string, int>> CountByTowerGroupedByStatusAsync(Guid towerId, CancellationToken cancellationToken = default);
}