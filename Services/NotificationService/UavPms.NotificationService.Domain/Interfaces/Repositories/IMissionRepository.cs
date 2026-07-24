using UavPms.NotificationService.Domain.Entities;

namespace UavPms.NotificationService.Domain.Interfaces.Repositories;

public interface IMissionRepository : IGenericRepository<Mission>
{
    Task<(IReadOnlyList<Mission> Items, int TotalCount)> GetMissionsPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? status);
    
    Task<IReadOnlyList<Mission>> GetMissionsByAssignedUserAsync(Guid userId);
    
    Task<Mission?> GetMissionDetailsByIdAsync(Guid id);
}