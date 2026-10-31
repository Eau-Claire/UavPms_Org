using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Tests.Common.Utilities;

/// <summary>
/// Users that pass the BR-18 re-validation done when people are put on a mission: Active, verified, a field role
/// and, when a region is given, a geographic scope for it.
/// </summary>
internal static class FieldUsers
{
    public static User Add(ApplicationDbContext db, Guid id, Guid? regionId = null, string role = UserRoles.Inspector)
    {
        var user = new User { Id = id, Status = "Active", IsEmailVerified = true, FullName = $"{role} {id.ToString()[..8]}" };
        user.UserRoles.Add(new UserRole { UserId = id, Role = new Role { RoleName = role } });
        db.Users.Add(user);
        if (regionId.HasValue) AddScope(db, id, regionId.Value);
        return user;
    }

    public static void AddScope(ApplicationDbContext db, Guid userId, Guid regionId) =>
        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = userId, RegionId = regionId });
}
