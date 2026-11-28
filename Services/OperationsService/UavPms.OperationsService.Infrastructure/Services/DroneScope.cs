using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Services;

/// <summary>
/// Drone người gọi được xem (spec §4): SystemAdmin mọi Drone; người khác Drone thuộc vùng của mình, hoặc Drone của
/// nhiệm vụ mình đang được giao (thành viên hiện trường có thể chưa được gán vùng). Dùng chung cho mọi API đọc Drone.
/// </summary>
internal static class DroneScope
{
    public static IQueryable<Uav> Visible(ApplicationDbContext db, ICurrentUserServices? current)
    {
        var drones = db.Uavs.Where(u => !u.IsDeleted);
        if (current == null || !current.IsAuthenticated || current.UserId == Guid.Empty)
            return drones.Where(_ => false);
        if (RegionScope.IsSystemAdmin(current))
            return drones;

        var userId = current.UserId;
        var regionIds = RegionScope.RegionIds(db, userId);
        var myMissionDroneIds = db.Missions
            .Where(m => m.UavId != null && m.Assignments.Any(a => a.UserId == userId && a.Status == MissionAssignmentStatus.Active))
            .Select(m => m.UavId!.Value);
        return drones.Where(u =>
            (u.AssignedRegionId != null && regionIds.Contains(u.AssignedRegionId.Value)) ||
            myMissionDroneIds.Contains(u.Id));
    }
}
