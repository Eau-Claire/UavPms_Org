using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class AssignmentAcceptPostponeTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId, string role)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        return mock;
    }

    private static ApplicationDbContext CreateContext(ICurrentUserServices user)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options, user);
    }

    [Fact]
    public async Task AcceptAssignment_TransitionsStatusToAccepted_AndSetsMissionToAssignedWhenAllRequiredAccepted()
    {
        var inspectorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(inspectorId, UserRoles.Inspector);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = inspectorId, Status = "Active" });
        db.Users.Add(new User { Id = managerId, Status = "Active" });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            Title = "Mission Pending Acceptance",
            ManagerId = managerId,
            Status = MissionStatus.PendingAcceptance
        };

        var assignment = new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = inspectorId,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Pending,
            IsRequired = true
        };
        mission.Assignments.Add(assignment);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        var result = await service.AcceptAssignmentAsync(mission.Id, CancellationToken.None);

        result.ResponseStatus.Should().Be(MissionAssignmentResponse.Accepted);
        result.RespondedAt.Should().NotBeNull();

        var updatedMission = await db.Missions.FindAsync(mission.Id);
        updatedMission!.Status.Should().Be(MissionStatus.Assigned);
        updatedMission.AcceptedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PostponeAssignment_RequiresReason_AndTransitionsToPostponed()
    {
        var inspectorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(inspectorId, UserRoles.Inspector);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = inspectorId, Status = "Active" });
        db.Users.Add(new User { Id = managerId, Status = "Active" });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            Title = "Mission Test Postpone",
            ManagerId = managerId,
            Status = MissionStatus.PendingAcceptance
        };

        var assignment = new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = inspectorId,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Pending,
            IsRequired = true
        };
        mission.Assignments.Add(assignment);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        // Empty reason throws
        var emptyAct = () => service.PostponeAssignmentAsync(mission.Id, "", CancellationToken.None);
        await emptyAct.Should().ThrowAsync<BusinessRuleException>().WithMessage("*POSTPONE_REASON_REQUIRED*");

        // Valid reason succeeds
        var result = await service.PostponeAssignmentAsync(mission.Id, "Bad weather forecast", CancellationToken.None);

        result.ResponseStatus.Should().Be(MissionAssignmentResponse.Postponed);
        result.ResponseReason.Should().Be("Bad weather forecast");

        var updatedMission = await db.Missions.FindAsync(mission.Id);
        updatedMission!.PostponedAt.Should().NotBeNull();
        updatedMission.PostponeReason.Should().Be("Bad weather forecast");
    }

    [Fact]
    public async Task CheckIn_Throws_IfAssignmentIsNotAccepted()
    {
        var inspectorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(inspectorId, UserRoles.Inspector);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = inspectorId, Status = "Active" });
        db.Users.Add(new User { Id = managerId, Status = "Active" });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            Title = "Mission CheckIn Test",
            ManagerId = managerId,
            Status = MissionStatus.Assigned
        };

        var assignment = new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = inspectorId,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Pending // Not yet accepted!
        };
        mission.Assignments.Add(assignment);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        var act = () => service.CheckInAsync(mission.Id, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*ASSIGNMENT_NOT_ACCEPTED*");
    }

    [Fact]
    public async Task CancelMission_ReleasesResourceBookings()
    {
        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var droneId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.Users.Add(new User { Id = inspectorId, Status = "Active" });

        var region = new Region { Id = Guid.NewGuid() };
        db.Regions.Add(region);
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = region.Id });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            Title = "Mission Cancel Test",
            ManagerId = managerId,
            RegionId = region.Id,
            Status = MissionStatus.PendingAcceptance
        };
        mission.Assignments.Add(new MissionAssignment { UserId = inspectorId, Status = MissionAssignmentStatus.Active });
        db.Missions.Add(mission);

        db.ResourceBookings.Add(new ResourceBooking
        {
            MissionId = mission.Id,
            UserId = inspectorId,
            StartAt = DateTime.UtcNow,
            EndAt = DateTime.UtcNow.AddHours(4),
            Status = ResourceBookingStatus.Active
        });
        db.ResourceBookings.Add(new ResourceBooking
        {
            MissionId = mission.Id,
            DroneId = droneId,
            StartAt = DateTime.UtcNow,
            EndAt = DateTime.UtcNow.AddHours(4),
            Status = ResourceBookingStatus.Active
        });
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        await service.CancelAsync(mission.Id, CancellationToken.None);

        var updatedMission = await db.Missions.FindAsync(mission.Id);
        updatedMission!.Status.Should().Be(MissionStatus.Cancelled);

        var bookings = await db.ResourceBookings.Where(b => b.MissionId == mission.Id).ToListAsync();
        bookings.Should().OnlyContain(b => b.Status == ResourceBookingStatus.Cancelled);
    }
}
