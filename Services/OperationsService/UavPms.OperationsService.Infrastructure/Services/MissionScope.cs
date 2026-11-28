using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Services;

/// <summary>
/// Nhiệm vụ người gọi được xem (spec §4), cùng luật với MissionLifecycleService.AccessibleMission:
/// SystemAdmin mọi nhiệm vụ; người khác chỉ nhiệm vụ mình phụ trách, được giao, hoặc thuộc vùng của mình.
/// Trả về IQueryable để dashboard, cảnh báo... lọc ngay trong SQL.
/// </summary>
internal static class MissionScope
{
    public static IQueryable<Mission> Visible(ApplicationDbContext db, ICurrentUserServices current)
    {
        if (RegionScope.IsSystemAdmin(current))
            return db.Missions;

        var userId = current.UserId;
        var regionIds = RegionScope.RegionIds(db, userId);
        return db.Missions.Where(m =>
            m.ManagerId == userId ||
            m.Assignments.Any(a => a.UserId == userId && a.Status == MissionAssignmentStatus.Active) ||
            (m.RegionId != null && regionIds.Contains(m.RegionId.Value)));
    }

    public static IQueryable<Guid> VisibleIds(ApplicationDbContext db, ICurrentUserServices current) =>
        Visible(db, current).Select(m => m.Id);
}
