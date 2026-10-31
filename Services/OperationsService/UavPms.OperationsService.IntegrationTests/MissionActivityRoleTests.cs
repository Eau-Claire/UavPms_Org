using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// Mission chat labels each sender by role; analysts and technicians used to show up as INSPECTOR,
/// and a ticket waiting for acceptance showed as InProgress on the mission's maintenance tab.
/// </summary>
[Collection(PostgresCollection.Name)]
public class MissionActivityRoleTests
{
    private readonly PostgresFixture _pg;

    public MissionActivityRoleTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Activities_LabelSendersByAssignmentOrAccountRole()
    {
        if (!_pg.Available) return;

        var tag = Guid.NewGuid().ToString("N")[..8];
        Guid managerId, analystId, technicianId, missionId;
        await using (var db = _pg.CreateContext())
        {
            managerId = await InsertUserAsync(db, $"m-{tag}@it.test", "Manager");
            analystId = await InsertUserAsync(db, $"a-{tag}@it.test", "Analyst");
            technicianId = await InsertUserAsync(db, $"t-{tag}@it.test", "MaintenanceTechnician");

            var region = new Region { Id = Guid.NewGuid(), Code = $"R-{tag}", Name = "Region", Type = "Province" };
            var mission = new Mission { Id = Guid.NewGuid(), MissionCode = $"M-{tag}", Title = "Mission", ManagerId = managerId, Status = MissionStatus.InProgress, RegionId = region.Id };
            missionId = mission.Id;
            db.AddRange(region, mission);
            db.Add(new MissionAssignment { Id = Guid.NewGuid(), MissionId = mission.Id, UserId = analystId, AssignmentRole = "analyst", AssignedById = managerId });
            db.AddRange(
                new MissionCommunicationLog { MissionId = mission.Id, SenderId = managerId, Content = "from manager" },
                new MissionCommunicationLog { MissionId = mission.Id, SenderId = analystId, Content = "from analyst" },
                new MissionCommunicationLog { MissionId = mission.Id, SenderId = technicianId, Content = "from technician" },
                new MissionCommunicationLog { MissionId = mission.Id, SenderId = null, Content = "system" });
            await db.SaveChangesAsync();
        }

        var current = new Mock<ICurrentUserServices>();
        current.SetupGet(u => u.UserId).Returns(managerId);
        current.SetupGet(u => u.IsAuthenticated).Returns(true);
        current.SetupGet(u => u.Roles).Returns(new[] { UserRoles.Manager });

        await using (var db = _pg.CreateContext(current.Object))
        {
            var service = new MissionLifecycleService(db, current.Object);

            var activities = await service.GetActivitiesAsync(missionId, default);
            activities.ToDictionary(a => a.Content, a => a.SenderRole).Should().BeEquivalentTo(new Dictionary<string, string>
            {
                ["from manager"] = "MANAGER",
                ["from analyst"] = "ANALYST",
                ["from technician"] = "TECHNICIAN",
                ["system"] = "SYSTEM",
            });

            var communications = await service.GetCommunicationsAsync(missionId, default);
            communications.Single(c => c.Content == "from technician").SenderRole.Should().Be("TECHNICIAN");
        }
    }

    private static async Task<Guid> InsertUserAsync(ApplicationDbContext db, string email, string role)
    {
        var userId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users" ("Id", "FullName", "Email", "Phone", "Status", "IsEmailVerified", "PasswordHash", "CreatedAt", "IsDeleted")
            VALUES ({userId}, 'IT user', {email}, '', 'Active', true, 'x', now(), false)
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
}
