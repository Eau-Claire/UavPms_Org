using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.OperationsService.Application.Features.Missions.Commands.DeleteMission;
using UavPms.OperationsService.Application.Features.Missions.Commands.UpdateMission;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class Phase4ResourceAndScopeTests
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
    public async Task DeleteMission_ThrowsInvalidMissionState_WhenNotDraft()
    {
        var managerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-DEL-01",
            Title = "Mission Assigned",
            RegionId = regionId,
            Status = MissionStatus.Assigned,
            ManagerId = managerId
        };
        db.Missions.Add(mission);
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = regionId });
        await db.SaveChangesAsync();

        var missionRepo = new MissionRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var handler = new DeleteMissionCommandHandler(unitOfWork, missionRepo, user.Object);

        var act = async () => await handler.Handle(new DeleteMissionCommand(mission.Id), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INVALID_MISSION_STATE");
    }

    [Fact]
    public async Task DeleteMission_ThrowsForbidden_WhenManagerLacksRegionScope()
    {
        var managerId = Guid.NewGuid();
        var otherRegionId = Guid.NewGuid();
        var missionRegionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-DEL-02",
            Title = "Draft Mission",
            RegionId = missionRegionId,
            Status = MissionStatus.Draft,
            ManagerId = managerId
        };
        db.Missions.Add(mission);
        // User only has scope for otherRegionId
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = otherRegionId });
        await db.SaveChangesAsync();

        var missionRepo = new MissionRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var handler = new DeleteMissionCommandHandler(unitOfWork, missionRepo, user.Object);

        var act = async () => await handler.Handle(new DeleteMissionCommand(mission.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("REGION_MANAGEMENT_SCOPE_REQUIRED");
    }

    [Fact]
    public async Task DeleteMission_Succeeds_WhenDraftAndManagerHasRegionScope()
    {
        var managerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-DEL-03",
            Title = "Draft Mission to Delete",
            RegionId = regionId,
            Status = MissionStatus.Draft,
            ManagerId = managerId
        };
        db.Missions.Add(mission);
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = regionId });
        await db.SaveChangesAsync();

        var missionRepo = new MissionRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var handler = new DeleteMissionCommandHandler(unitOfWork, missionRepo, user.Object);

        await handler.Handle(new DeleteMissionCommand(mission.Id), CancellationToken.None);

        var deleted = await db.Missions.FindAsync(mission.Id);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateMission_ThrowsForbidden_WhenManagerLacksRegionScope()
    {
        var managerId = Guid.NewGuid();
        var otherRegionId = Guid.NewGuid();
        var missionRegionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-UPD-01",
            Title = "Mission Initial",
            RegionId = missionRegionId,
            Status = MissionStatus.Draft,
            ManagerId = managerId
        };
        db.Missions.Add(mission);
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = otherRegionId });
        await db.SaveChangesAsync();

        var missionRepo = new MissionRepository(db);
        var userRepo = new Mock<IUserRepository>();
        var uavRepo = new Mock<IUavRepository>();
        var unitOfWork = new UnitOfWork(db);
        var handler = new UpdateMissionCommandHandler(missionRepo, userRepo.Object, uavRepo.Object, unitOfWork, user.Object);

        var act = async () => await handler.Handle(new UpdateMissionCommand(mission.Id, "Updated Title", "Updated Route", Guid.NewGuid(), "UAV-1", "Draft", "Updated Desc"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("REGION_MANAGEMENT_SCOPE_REQUIRED");
    }

    [Fact]
    public async Task UpdateMission_ThrowsBusinessRule_WhenMissionStarted()
    {
        var managerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-UPD-02",
            Title = "Mission InProgress",
            RegionId = regionId,
            Status = MissionStatus.InProgress,
            ManagerId = managerId
        };
        db.Missions.Add(mission);
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = regionId });
        await db.SaveChangesAsync();

        var missionRepo = new MissionRepository(db);
        var userRepo = new Mock<IUserRepository>();
        var uavRepo = new Mock<IUavRepository>();
        var unitOfWork = new UnitOfWork(db);
        var handler = new UpdateMissionCommandHandler(missionRepo, userRepo.Object, uavRepo.Object, unitOfWork, user.Object);

        var act = async () => await handler.Handle(new UpdateMissionCommand(mission.Id, "Updated Title", "Updated Route", Guid.NewGuid(), "UAV-1", "InProgress", "Updated Desc"), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("MISSION_IMMUTABLE_AFTER_START");
    }

    [Fact]
    public async Task AssignDroneAsync_RejectsConflict_WhenDroneBookedInOverlappingTimeWindow()
    {
        var managerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var droneId = Guid.NewGuid();
        var otherMissionId = Guid.NewGuid();
        var targetMissionId = Guid.NewGuid();

        var user = CreateUserMock(managerId, UserRoles.SystemAdmin);
        await using var db = CreateContext(user.Object);

        var now = DateTime.UtcNow;
        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.Regions.Add(new Region { Id = regionId, Code = "REG-01" });
        db.Uavs.Add(new Uav { Id = droneId, UavCode = "DRONE-01", Model = "Matrice 300", Status = DroneStatus.Idle });

        var otherMission = new Mission
        {
            Id = otherMissionId,
            MissionCode = "MS-OTHER-01",
            Title = "Other Mission",
            RegionId = regionId,
            Status = MissionStatus.Assigned,
            ManagerId = managerId
        };
        db.Missions.Add(otherMission);

        // Existing booking for other mission from now to now + 2h
        db.ResourceBookings.Add(new ResourceBooking
        {
            MissionId = otherMissionId,
            DroneId = droneId,
            StartAt = now.AddHours(1),
            EndAt = now.AddHours(3),
            Status = ResourceBookingStatus.Active
        });

        // Target mission with overlapping planned window from now to now + 2h
        var targetMission = new Mission
        {
            Id = targetMissionId,
            MissionCode = "MS-TGT-01",
            Title = "Target Mission",
            RegionId = regionId,
            Status = MissionStatus.Assigned,
            PlannedStart = now,
            PlannedEnd = now.AddHours(2),
            ManagerId = managerId
        };
        db.Missions.Add(targetMission);
        await db.SaveChangesAsync();

        var notifier = new Mock<IMissionRealtimeNotifier>();
        var service = new MissionLifecycleService(db, user.Object, notifier.Object);

        var act = async () => await service.AssignDroneAsync(targetMissionId, droneId, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("RESOURCE_BOOKING_CONFLICT");
    }

    [Fact]
    public async Task AssignDroneAsync_SucceedsAndCreatesBooking_WhenNoConflict()
    {
        var managerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var droneId = Guid.NewGuid();
        var targetMissionId = Guid.NewGuid();

        var user = CreateUserMock(managerId, UserRoles.SystemAdmin);
        await using var db = CreateContext(user.Object);

        var now = DateTime.UtcNow;
        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.Regions.Add(new Region { Id = regionId, Code = "REG-01" });
        db.Uavs.Add(new Uav { Id = droneId, UavCode = "DRONE-FREE", Model = "Matrice 350", Status = DroneStatus.Idle });

        var targetMission = new Mission
        {
            Id = targetMissionId,
            MissionCode = "MS-FREE-01",
            Title = "Target Mission Free",
            RegionId = regionId,
            Status = MissionStatus.Assigned,
            PlannedStart = now,
            PlannedEnd = now.AddHours(2),
            ManagerId = managerId
        };
        db.Missions.Add(targetMission);
        await db.SaveChangesAsync();

        var notifier = new Mock<IMissionRealtimeNotifier>();
        var service = new MissionLifecycleService(db, user.Object, notifier.Object);

        await service.AssignDroneAsync(targetMissionId, droneId, CancellationToken.None);

        targetMission.UavId.Should().Be(droneId);
        var booking = await db.ResourceBookings.FirstOrDefaultAsync(b => b.MissionId == targetMissionId && b.DroneId == droneId);
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(ResourceBookingStatus.Active);
        booking.StartAt.Should().Be(targetMission.PlannedStart!.Value);
        booking.EndAt.Should().Be(targetMission.PlannedEnd!.Value);
    }
}
