using UavPms.AIInspectionService.Domain.Common;
using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Domain.Interfaces.Repositories;

public interface IAnomalyRepository : IGenericRepository<DetectedAnomaly>
{
    Task<DetectedAnomaly?> GetByIdWithDetailAsync(Guid id);
    /// <summary>Defect hiện hành (chưa bị thay thế) mà người gọi được xem.</summary>
    Task<IReadOnlyList<DetectedAnomaly>> GetAllWithDetailsAsync(MissionVisibility visibility);
    /// <summary>Defect đang chờ duyệt mà người gọi được xem.</summary>
    Task<IReadOnlyList<DetectedAnomaly>> GetPendingWithDetailsAsync(MissionVisibility visibility);
    Task<IReadOnlyList<DetectedAnomaly>> GetByTowerIdWithDetailsAsync(Guid towerId);
}
