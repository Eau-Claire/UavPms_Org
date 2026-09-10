using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IMissionRepository : IGenericRepository<Mission>
{
    Task<(IReadOnlyList<Mission> Items, int TotalCount)> GetMissionsPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        string? sortBy = "createdAt",
        bool sortDescending = true);
    
    Task<IReadOnlyList<Mission>> GetMissionsByAssignedUserAsync(Guid userId);
    
    Task<Mission?> GetMissionDetailsByIdAsync(Guid id);
}
