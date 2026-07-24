using UavPms.NotificationService.Domain.Entities;

namespace UavPms.NotificationService.Domain.Interfaces.Repositories;

public interface IAnomalyRepository : IGenericRepository<DetectedAnomaly>
{
    Task<DetectedAnomaly?> GetByIdWithDetailAsync(Guid id);
    Task<IReadOnlyList<DetectedAnomaly>> GetAllWithDetailsAsync();
    Task<IReadOnlyList<DetectedAnomaly>> GetPendingWithDetailsAsync();
    Task<IReadOnlyList<DetectedAnomaly>> GetByAssetIdWithDetailsAsync(Guid assetId);
}