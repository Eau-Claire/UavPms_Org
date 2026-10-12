using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class MissionDroneExecutionTests
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<ICurrentUserServices> _currentMock;
    private readonly Mock<IMissionRealtimeNotifier> _notifierMock;
    private readonly MissionLifecycleService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _regionId = Guid.NewGuid();
    private readonly Guid _droneId = Guid.NewGuid();

    public MissionDroneExecutionTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"DroneExecutionTests_{Guid.NewGuid()}")
            .Options;

        _currentMock = new Mock<ICurrentUserServices>();
        _currentMock.Setup(c => c.IsAuthenticated).Returns(true);
        _currentMock.Setup(c => c.UserId).Returns(_userId);
        _currentMock.Setup(c => c.Roles).Returns(new[] { UserRoles.Inspector });

        _db = new ApplicationDbContext(options, _currentMock.Object);

        _notifierMock = new Mock<IMissionRealtimeNotifier>();

        _service = new MissionLifecycleService(_db, _currentMock.Object, _notifierMock.Object);

        _db.Users.Add(new User
        {
            Id = _userId,
            Email = "inspector@test.com",
            FullName = "Mission Pilot",
            Status = "Active"
        });

        _db.Users.Add(new User
        {
            Id = _managerId,
            Email = "manager@test.com",
            FullName = "Mission Manager",
            Status = "Active"
        });

        _db.Regions.Add(new Region
        {
            Id = _regionId,
            RegionName = "Mekong Grid Area",
            IsDeleted = false
        });

        _db.UserGeographicScopes.Add(new UserGeographicScope
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            RegionId = _regionId
        });

        _db.Uavs.Add(new Uav
        {
            Id = _droneId,
            UavCode = "UAV-DRONE-01",
            Model = "DJI Matrice 300 RTK",
            Status = DroneStatus.Idle,
            BatteryLevel = 98.0
        });

        _db.SaveChanges();
    }

    [Fact]
    public async Task StartAsync_ShouldTransitionToInProgress_AndUpdateUavStatusToFlying()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-FLY-01",
            Title = "Flight Execution Test",
            RegionId = _regionId,
            ManagerId = _managerId,
            InspectorId = _userId,
            UavId = _droneId,
            Status = MissionStatus.Ready
        };
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();

        // Act
        await _service.StartAsync(missionId, CancellationToken.None);

        // Assert
        mission.Status.Should().Be(MissionStatus.InProgress);
        mission.StartedAt.Should().NotBeNull();

        var drone = await _db.Uavs.FindAsync(_droneId);
        drone!.Status.Should().Be(DroneStatus.Flying);

        _notifierMock.Verify(n => n.NotifyAsync(
            It.Is<MissionLifecycleEventDto>(e => e.MissionId == missionId.ToString() && e.Type == "STARTED" && e.Status == "IN_PROGRESS"),
            It.IsAny<CancellationToken>()), Times.Once);

        var audit = await _db.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == missionId && a.ActionType == "MISSION_STARTED");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_ShouldThrowBusinessRuleException_WhenMissionIsNotReady()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-NOTREADY-01",
            Title = "Not Ready Mission",
            RegionId = _regionId,
            InspectorId = _userId,
            UavId = _droneId,
            Status = MissionStatus.Assigned // not Ready
        };
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await _service.StartAsync(missionId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*INVALID_MISSION_STATE*");
    }

    [Fact]
    public async Task CompleteAsync_ShouldTransitionToCompleted_ReleaseBookings_AndReturnDroneToIdle()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-COMP-01",
            Title = "Complete Test Mission",
            RegionId = _regionId,
            ManagerId = _managerId,
            InspectorId = _userId,
            UavId = _droneId,
            Status = MissionStatus.InProgress,
            StartedAt = DateTime.UtcNow.AddHours(-1)
        };
        _db.Missions.Add(mission);

        // Set drone flying
        var drone = await _db.Uavs.FindAsync(_droneId);
        drone!.Status = DroneStatus.Flying;

        // Add active resource booking
        _db.ResourceBookings.Add(new ResourceBooking
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            DroneId = _droneId,
            StartAt = DateTime.UtcNow.AddHours(-1),
            EndAt = DateTime.UtcNow.AddHours(1),
            Status = ResourceBookingStatus.Active
        });

        await _db.SaveChangesAsync();

        // Act
        await _service.CompleteAsync(missionId, CancellationToken.None);

        // Assert
        mission.Status.Should().Be(MissionStatus.Completed);
        mission.EndedAt.Should().NotBeNull();

        drone.Status.Should().Be(DroneStatus.Idle);

        var booking = await _db.ResourceBookings.FirstOrDefaultAsync(b => b.MissionId == missionId);
        booking!.Status.Should().Be(ResourceBookingStatus.Released);

        _notifierMock.Verify(n => n.NotifyAsync(
            It.Is<MissionLifecycleEventDto>(e => e.MissionId == missionId.ToString() && e.Type == "COMPLETED" && e.Status == "COMPLETED"),
            It.IsAny<CancellationToken>()), Times.Once);

        var audit = await _db.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == missionId && a.ActionType == "MISSION_COMPLETED");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task ReturnDroneHandoverAsync_ShouldUpdateHandoverAndSetDroneIdle()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-HANDOVER-RET",
            Title = "Return Handover Mission",
            RegionId = _regionId,
            InspectorId = _userId,
            UavId = _droneId,
            Status = MissionStatus.Completed,
            EndedAt = DateTime.UtcNow
        };

        var initialHandover = new DroneHandover
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            DroneId = _droneId,
            HandedOverBy = _managerId,
            ReceivedBy = _userId,
            ReceivedAt = DateTime.UtcNow.AddHours(-3),
            Condition = "Good condition at dispatch",
            Status = DroneHandoverStatus.Accepted
        };
        mission.DroneHandovers.Add(initialHandover);
        _db.Missions.Add(mission);

        var drone = await _db.Uavs.FindAsync(_droneId);
        drone!.Status = DroneStatus.Flying;

        await _db.SaveChangesAsync();

        // Act
        var result = await _service.ReturnDroneHandoverAsync(missionId, _droneId, "Returned safely with 25% battery remaining", CancellationToken.None);

        // Assert
        result.ReturnedAt.Should().NotBeNull();
        result.Condition.Should().Contain("Returned safely with 25% battery remaining");

        drone.Status.Should().Be(DroneStatus.Idle);

        var audit = await _db.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == missionId && a.ActionType == "DRONE_HANDOVER_RETURNED");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task ReturnDroneHandoverAsync_ShouldThrowNotFound_WhenNoActiveHandoverFound()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-NOHANDOVER",
            Title = "No Handover Mission",
            RegionId = _regionId,
            InspectorId = _userId,
            UavId = _droneId,
            Status = MissionStatus.Completed
        };
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await _service.ReturnDroneHandoverAsync(missionId, _droneId, "Condition", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task StartAsync_ShouldThrowForbiddenException_WhenInspectorIsNotAssignedToMissionEvenInSameRegion()
    {
        // Arrange
        var anotherInspectorId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        // Add scope for current user in _regionId so they pass AccessibleMission
        _db.UserGeographicScopes.Add(new UserGeographicScope
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            RegionId = _regionId
        });

        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-IDOR-START",
            Title = "Another Inspector Mission",
            RegionId = _regionId,
            InspectorId = anotherInspectorId,
            UavId = _droneId,
            Status = MissionStatus.Assigned
        };
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await _service.StartAsync(missionId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("USER_NOT_ASSIGNED_TO_MISSION");
    }

    [Fact]
    public async Task CompleteAsync_ShouldThrowForbiddenException_WhenInspectorIsNotAssignedToMissionEvenInSameRegion()
    {
        // Arrange
        var anotherInspectorId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        _db.UserGeographicScopes.Add(new UserGeographicScope
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            RegionId = _regionId
        });

        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-IDOR-COMPLETE",
            Title = "Another Inspector Mission",
            RegionId = _regionId,
            InspectorId = anotherInspectorId,
            UavId = _droneId,
            Status = MissionStatus.InProgress
        };
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await _service.CompleteAsync(missionId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("USER_NOT_ASSIGNED_TO_MISSION");
    }
}
