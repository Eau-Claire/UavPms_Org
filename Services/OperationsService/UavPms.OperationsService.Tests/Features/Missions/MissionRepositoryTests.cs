using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class MissionRepositoryTests
{
    private static ApplicationDbContext CreateContext()
    {
        var userMock = new Mock<ICurrentUserServices>();
        userMock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        userMock.SetupGet(u => u.IsAuthenticated).Returns(true);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options, userMock.Object);
    }

    [Fact]
    public async Task GetMissionsByAssignedUserAsync_ShouldExcludeMissionsWhereAssignmentIsRevoked()
    {
        await using var db = CreateContext();
        var pilotId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = pilotId, FullName = "Pilot John", Email = "pilot@example.com", Status = "Active" },
            new User { Id = otherUserId, FullName = "Other User", Email = "other@example.com", Status = "Active" },
            new User { Id = managerId, FullName = "Manager Alice", Email = "manager@example.com", Status = "Active" }
        );

        // Mission 1: Pilot has Revoked assignment, not inspector, not manager
        var mission1 = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MSN-REVOKED",
            Title = "Revoked Mission",
            ManagerId = managerId,
            Status = MissionStatus.PendingAcceptance
        };
        mission1.Assignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = mission1.Id,
            UserId = pilotId,
            AssignmentRole = "PILOT",
            Status = MissionAssignmentStatus.Revoked,
            ResponseStatus = MissionAssignmentResponse.Pending
        });

        // Mission 2: Pilot has Active assignment
        var mission2 = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MSN-ACTIVE",
            Title = "Active Mission",
            ManagerId = managerId,
            Status = MissionStatus.Assigned
        };
        mission2.Assignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = mission2.Id,
            UserId = pilotId,
            AssignmentRole = "PILOT",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Accepted
        });
        // Also add a revoked assignment for other user on mission 2
        mission2.Assignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = mission2.Id,
            UserId = otherUserId,
            AssignmentRole = "OBSERVER",
            Status = MissionAssignmentStatus.Revoked,
            ResponseStatus = MissionAssignmentResponse.Pending
        });

        db.Missions.AddRange(mission1, mission2);
        await db.SaveChangesAsync();

        var repo = new MissionRepository(db);

        // Act
        var results = await repo.GetMissionsByAssignedUserAsync(pilotId);

        // Assert
        results.Should().HaveCount(1);
        results[0].MissionCode.Should().Be("MSN-ACTIVE");
        // Verify only active assignments are loaded in the navigation property
        results[0].Assignments.Should().OnlyContain(a => a.Status == MissionAssignmentStatus.Active);
    }
}
