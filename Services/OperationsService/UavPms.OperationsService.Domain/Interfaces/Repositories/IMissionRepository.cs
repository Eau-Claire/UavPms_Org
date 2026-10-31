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
        bool sortDescending = true,
        string? priority = null,
        IReadOnlyList<Guid?>? allowedRegionIds = null);
    
    Task<IReadOnlyList<Mission>> GetMissionsByAssignedUserAsync(Guid userId);
    
    Task<Mission?> GetMissionDetailsByIdAsync(Guid id);
    Task<bool> UserCanAccessAsync(Guid missionId, Guid userId, bool global, CancellationToken cancellationToken);
    Task<bool> UserCanManageAsync(Guid missionId, Guid userId, bool global, CancellationToken cancellationToken);

    /// <summary>Cancels the mission's Active resource bookings (saved with the unit of work).</summary>
    Task CancelActiveBookingsAsync(Guid missionId, CancellationToken cancellationToken = default);

    /// <summary>Ghi danh sách lỗi ưu tiên (mã hoặc tên DefectCategories) vào bảng MissionPriorityDefects; mã không tồn tại bị bỏ qua.</summary>
    Task SetPriorityDefectsAsync(Mission mission, IEnumerable<string>? categoryCodes, CancellationToken cancellationToken = default);
}
