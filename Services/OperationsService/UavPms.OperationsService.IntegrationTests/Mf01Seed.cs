using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>Seed helpers for the MF01 (readiness assessment / technical inspection) tests.</summary>
internal static class Mf01Seed
{
    public static string Tag() => Guid.NewGuid().ToString("N")[..8];

    public static ICurrentUserServices Current(Guid userId, string role)
    {
        var current = new Mock<ICurrentUserServices>();
        current.SetupGet(u => u.UserId).Returns(userId);
        current.SetupGet(u => u.IsAuthenticated).Returns(true);
        current.SetupGet(u => u.Roles).Returns(new[] { role });
        current.SetupGet(u => u.Username).Returns(role);
        return current.Object;
    }

    /// <summary>Users carry PasswordHash, which only IdentityService maps, so they are inserted in SQL.</summary>
    public static async Task<Guid> InsertUserAsync(ApplicationDbContext db, string tag, string role, string status = "Active", bool verified = true)
    {
        var userId = Guid.NewGuid();
        var email = $"{role.ToLowerInvariant()}-{userId:N}@{tag}.it.test";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users" ("Id", "FullName", "Email", "Phone", "Status", "IsEmailVerified", "PasswordHash", "CreatedAt", "IsDeleted")
            VALUES ({userId}, {role + " " + tag}, {email}, '', {status}, {verified}, 'x', now(), false)
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Roles" ("RoleName", "Description") VALUES ({role}, '') ON CONFLICT ("RoleName") DO NOTHING
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserRoles" ("UserId", "RoleId", "AssignedAt")
            SELECT {userId}, "Id", now() FROM "Roles" WHERE "RoleName" = {role}
            """);
        return userId;
    }

    public static async Task AddScopeAsync(ApplicationDbContext db, Guid userId, Guid regionId)
    {
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = userId, RegionId = regionId });
        await db.SaveChangesAsync();
    }

    /// <summary>Region, towers ~110 m apart (or <paramref name="spacingDeg"/> degrees of latitude), a manager with scope,
    /// and one eligible Inspector, Analyst and Technician in scope.</summary>
    public static async Task<Site> SeedSiteAsync(ApplicationDbContext db, int towerCount = 2, double spacingDeg = 0.001)
    {
        var tag = Tag();
        var region = new Region { Id = Guid.NewGuid(), Code = $"R-{tag}", Name = $"Region {tag}", Type = "Province" };
        var towers = Enumerable.Range(0, towerCount)
            .Select(i => new Tower
            {
                Id = Guid.NewGuid(),
                RegionId = region.Id,
                TowerCode = $"T-{tag}-{i}",
                Geom = new Point(105.85, 21.03 + i * spacingDeg) { SRID = 4326 }
            })
            .ToList();
        db.Add(region);
        db.AddRange(towers);
        await db.SaveChangesAsync();

        var managerId = await InsertUserAsync(db, tag, "Manager");
        var inspectorId = await InsertUserAsync(db, tag, "Inspector");
        var analystId = await InsertUserAsync(db, tag, "Analyst");
        var technicianId = await InsertUserAsync(db, tag, "MaintenanceTechnician");
        foreach (var id in new[] { managerId, inspectorId, analystId, technicianId })
            db.UserGeographicScopes.Add(new UserGeographicScope { UserId = id, RegionId = region.Id });
        await db.SaveChangesAsync();

        return new Site(tag, region.Id, towers.Select(t => t.Id).ToList(), managerId, inspectorId, analystId, technicianId);
    }

    public static async Task<Guid> AddDroneAsync(
        ApplicationDbContext db,
        string tag,
        string suffix,
        DroneOperationalStatus status = DroneOperationalStatus.Available,
        TechnicalHealth storedHealth = TechnicalHealth.Healthy,
        double enduranceMinutes = 40,
        double rangeKm = 10,
        params (DroneTechnicalInspectionStatus Status, TechnicalHealth Health, DateTime CompletedAt, DateTime ValidUntil)[] inspections)
    {
        var drone = new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = $"UAV-{tag}-{suffix}",
            Name = $"Drone {suffix}",
            Model = "M300",
            SerialNumber = $"SN-{tag}-{suffix}",
            OperationalStatus = status,
            TechnicalHealth = storedHealth,
            FlightEnduranceMinutes = enduranceMinutes,
            OperationalRangeKm = rangeKm,
            BatteryLevel = 90
        };
        db.Uavs.Add(drone);
        foreach (var i in inspections)
        {
            db.DroneTechnicalInspections.Add(new DroneTechnicalInspection
            {
                DroneId = drone.Id,
                Status = i.Status,
                Health = i.Health,
                StartedAt = i.CompletedAt,
                CompletedAt = i.CompletedAt,
                ValidUntil = i.ValidUntil,
                PolicyVersion = "v2.0"
            });
        }
        await db.SaveChangesAsync();
        return drone.Id;
    }

    public static (DroneTechnicalInspectionStatus, TechnicalHealth, DateTime, DateTime) ValidPassed(int daysAgo = 1) =>
        (DroneTechnicalInspectionStatus.Passed, TechnicalHealth.Healthy, DateTime.UtcNow.AddDays(-daysAgo), DateTime.UtcNow.AddDays(-daysAgo).AddDays(7));

    public sealed record Site(string Tag, Guid RegionId, List<Guid> TowerIds, Guid ManagerId, Guid InspectorId, Guid AnalystId, Guid TechnicianId);
}
