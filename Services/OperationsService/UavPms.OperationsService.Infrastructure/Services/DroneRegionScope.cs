using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Services;

/// <summary>
/// Phạm vi truy cập Drone (BE-REQ-101): lấy trực tiếp theo Uav.AssignedRegionId, không suy ra từ Mission/Assessment.
/// Dùng chung cho kiểm tra BIST, Flight Controller và lịch sử kiểm định để mọi nơi cùng một quy tắc.
/// </summary>
internal static class DroneRegionScope
{
    public static Task EnsureAsync(ApplicationDbContext db, ICurrentUserServices current, Uav drone, CancellationToken ct) =>
        EnsureRegionAsync(db, current, drone.AssignedRegionId, ct);

    /// <summary>Vùng phải nằm trong scope của người gọi (SystemAdmin bỏ qua); vùng null (Drone chưa gắn vùng) bị từ chối.</summary>
    public static async Task EnsureRegionAsync(ApplicationDbContext db, ICurrentUserServices current, Guid? regionId, CancellationToken ct)
    {
        // SystemAdmin toàn quyền; Drone chưa gắn vùng bị từ chối để tránh lộ Drone cho mọi vùng.
        if (!await RegionScope.IsGrantedAsync(db, current, regionId, ct))
            throw new ForbiddenException("DRONE_ACCESS_DENIED");
    }
}
