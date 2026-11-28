using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

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
        // SystemAdmin toàn quyền, không lọc UserGeographicScopes.
        if (current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase))
            return;

        // Drone chưa gắn vùng thì không có scope nào khớp nên từ chối, tránh lộ Drone cho mọi vùng.
        if (regionId is not Guid droneRegionId)
            throw new ForbiddenException("DRONE_ACCESS_DENIED");

        var grantedRegionIds = await db.UserGeographicScopes
            .Where(s => s.UserId == current.UserId)
            .Select(s => s.RegionId)
            .ToListAsync(ct);
        if (grantedRegionIds.Contains(droneRegionId))
            return;

        // Quy ước hiện hữu của GeographicAccessFilter: scope vùng cha bao phủ vùng con trực tiếp.
        var parentRegionId = await db.Regions
            .Where(r => r.Id == droneRegionId)
            .Select(r => r.ParentId)
            .FirstOrDefaultAsync(ct);
        if (parentRegionId.HasValue && grantedRegionIds.Contains(parentRegionId.Value))
            return;

        throw new ForbiddenException("DRONE_ACCESS_DENIED");
    }
}
