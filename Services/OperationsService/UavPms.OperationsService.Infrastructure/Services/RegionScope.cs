using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Infrastructure.Services;

/// <summary>
/// Phạm vi vùng của người gọi (spec §4): vùng được gán trong UserGeographicScopes cộng vùng con trực tiếp
/// (cùng quy ước với GeographicAccessFilter). SystemAdmin không bị giới hạn.
/// </summary>
internal static class RegionScope
{
    public static bool IsSystemAdmin(ICurrentUserServices current) =>
        current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase);

    public static bool HasRole(ICurrentUserServices current, string role) =>
        current.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>Id các vùng người dùng được truy cập, dạng IQueryable để lọc ngay trong SQL.</summary>
    public static IQueryable<Guid> RegionIds(ApplicationDbContext db, Guid userId)
    {
        var granted = db.UserGeographicScopes.Where(s => s.UserId == userId).Select(s => s.RegionId);
        var children = db.Regions
            .Where(r => r.ParentId != null && granted.Contains(r.ParentId.Value))
            .Select(r => r.Id);
        return granted.Union(children);
    }

    /// <summary>Vùng có thuộc phạm vi người gọi không. Vùng null (chưa gắn vùng) chỉ SystemAdmin truy cập được.</summary>
    public static async Task<bool> IsGrantedAsync(ApplicationDbContext db, ICurrentUserServices current, Guid? regionId, CancellationToken ct)
    {
        if (IsSystemAdmin(current))
            return true;
        if (regionId is not Guid id)
            return false;
        return await RegionIds(db, current.UserId).ContainsAsync(id, ct);
    }
}
