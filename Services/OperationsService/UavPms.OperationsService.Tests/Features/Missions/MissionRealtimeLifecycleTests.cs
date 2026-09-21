using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class MissionRealtimeLifecycleTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId, string role, string username = "TestUser")
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        mock.SetupGet(u => u.Username).Returns(username);
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
    public async Task ConfirmMissionAsync_ShouldUpdateStatusAndTriggerRealtime()
    {
        // Arrange
        var inspectorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(inspectorId, UserRoles.Inspector, "Phi Công A");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = inspectorId, Status = "Active" });
        db.Users.Add(new User { Id = managerId, Status = "Active" });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-CONFIRM-01",
            Title = "Mission Pending Acceptance",
            ManagerId = managerId,
            Status = MissionStatus.PendingAcceptance
        };
        mission.Assignments.Add(new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = inspectorId,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Pending,
            IsRequired = true
        });
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var notifierMock = new Mock<IMissionRealtimeNotifier>();
        var service = new MissionLifecycleService(db, user.Object, notifierMock.Object);

        // Act
        var result = await service.ConfirmMissionAsync(mission.Id, "Đã sẵn sàng thực hiện", CancellationToken.None);

        // Assert
        result.Status.Should().Be(MissionStatus.Confirmed);
        notifierMock.Verify(n => n.NotifyAsync(
            It.Is<MissionLifecycleEventDto>(e => e.MissionId == mission.Id.ToString() && e.Type == "CONFIRMED"),
            It.IsAny<CancellationToken>()), Times.Once);

        var commLog = await db.MissionCommunicationLogs.FirstOrDefaultAsync(l => l.MissionId == mission.Id);
        commLog.Should().NotBeNull();
        commLog!.Type.Should().Be("CONFIRM");
    }

    [Fact]
    public async Task SuspendAndResumeMission_ShouldTransitionStatusesAndTriggerRealtime()
    {
        // Arrange
        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager, "Quản Lý B");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.Users.Add(new User { Id = inspectorId, Status = "Active" });
        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = managerId, RegionId = regionId });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-SUSPEND-01",
            Title = "Mission In Progress",
            ManagerId = managerId,
            RegionId = regionId,
            Status = MissionStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };
        mission.Assignments.Add(new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = inspectorId,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active
        });
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var notifierMock = new Mock<IMissionRealtimeNotifier>();
        var service = new MissionLifecycleService(db, user.Object, notifierMock.Object);

        // Act - Suspend
        var suspended = await service.SuspendMissionAsync(mission.Id, "Thời tiết xấu, gió to", CancellationToken.None);
        suspended.Status.Should().Be(MissionStatus.Suspended);
        notifierMock.Verify(n => n.NotifyAsync(
            It.Is<MissionLifecycleEventDto>(e => e.MissionId == mission.Id.ToString() && e.Type == "SUSPENDED"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Act - Resume
        var resumed = await service.ResumeMissionAsync(mission.Id, "Thời tiết đã ổn định", CancellationToken.None);
        resumed.Status.Should().Be(MissionStatus.InProgress);
        notifierMock.Verify(n => n.NotifyAsync(
            It.Is<MissionLifecycleEventDto>(e => e.MissionId == mission.Id.ToString() && e.Type == "RESUMED"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostponeMission_ShouldUpdateStatusAndRecordReason()
    {
        // Arrange
        var managerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager, "Quản Lý C");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = managerId, RegionId = regionId });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-POSTPONE-01",
            Title = "Mission Ready",
            ManagerId = managerId,
            RegionId = regionId,
            Status = MissionStatus.Ready
        };
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var notifierMock = new Mock<IMissionRealtimeNotifier>();
        var service = new MissionLifecycleService(db, user.Object, notifierMock.Object);

        // Act
        var postponed = await service.PostponeMissionAsync(mission.Id, "Đường dây đang bảo trì khẩn cấp", CancellationToken.None);

        // Assert
        postponed.Status.Should().Be(MissionStatus.Postponed);
        notifierMock.Verify(n => n.NotifyAsync(
            It.Is<MissionLifecycleEventDto>(e => e.MissionId == mission.Id.ToString() && e.Type == "POSTPONED"),
            It.IsAny<CancellationToken>()), Times.Once);

        var log = await db.MissionCommunicationLogs.FirstOrDefaultAsync(l => l.MissionId == mission.Id && l.Type == "POSTPONE");
        log.Should().NotBeNull();
        log!.Content.Should().Contain("Đường dây đang bảo trì khẩn cấp");
    }

    [Fact]
    public async Task CancelMission_ShouldUpdateStatusAndTriggerRealtime()
    {
        // Arrange
        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager, "Quản Lý D");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.Users.Add(new User { Id = inspectorId, Status = "Active" });
        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = managerId, RegionId = regionId });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-CANCEL-01",
            Title = "Mission to Cancel",
            ManagerId = managerId,
            RegionId = regionId,
            Status = MissionStatus.Assigned
        };
        mission.Assignments.Add(new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = inspectorId,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active
        });
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var notifierMock = new Mock<IMissionRealtimeNotifier>();
        var service = new MissionLifecycleService(db, user.Object, notifierMock.Object);

        // Act
        var cancelled = await service.CancelMissionAsync(mission.Id, "Hủy theo yêu cầu điều độ", CancellationToken.None);

        // Assert
        cancelled.Status.Should().Be(MissionStatus.Cancelled);
        notifierMock.Verify(n => n.NotifyAsync(
            It.Is<MissionLifecycleEventDto>(e => e.MissionId == mission.Id.ToString() && e.Type == "CANCELLED"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemindMissionAsync_ShouldTriggerReminderRealtimeAndNotification()
    {
        // Arrange
        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager, "Quản Lý E");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.Users.Add(new User { Id = inspectorId, Status = "Active" });
        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = managerId, RegionId = regionId });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-REMIND-01",
            Title = "Mission Pending Acceptance",
            ManagerId = managerId,
            RegionId = regionId,
            Status = MissionStatus.PendingAcceptance,
            ConfirmationDeadline = DateTime.UtcNow.AddHours(2)
        };
        mission.Assignments.Add(new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = inspectorId,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Pending,
            IsRequired = true
        });
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var notifierMock = new Mock<IMissionRealtimeNotifier>();
        var service = new MissionLifecycleService(db, user.Object, notifierMock.Object);

        // Act
        await service.RemindMissionAsync(mission.Id, "Vui lòng xác nhận trước 12h", CancellationToken.None);

        // Assert
        notifierMock.Verify(n => n.NotifyAsync(
            It.Is<MissionLifecycleEventDto>(e => e.MissionId == mission.Id.ToString() && e.Type == "REMINDER"),
            It.IsAny<CancellationToken>()), Times.Once);

        var notif = await db.Notifications.FirstOrDefaultAsync(n => n.UserId == inspectorId);
        notif.Should().NotBeNull();
        notif!.Type.Should().Be("MISSION_REMINDER");
    }

    [Fact]
    public async Task AddAndGetCommunications_ShouldPersistAndReturnOrdered()
    {
        // Arrange
        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager, "Quản Lý F");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.Users.Add(new User { Id = inspectorId, Status = "Active" });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-COMM-01",
            Title = "Mission Communication Test",
            ManagerId = managerId,
            Status = MissionStatus.Assigned
        };
        mission.Assignments.Add(new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = inspectorId,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active
        });
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var notifierMock = new Mock<IMissionRealtimeNotifier>();
        var service = new MissionLifecycleService(db, user.Object, notifierMock.Object);

        // Act - Add communication
        var log = await service.AddCommunicationAsync(mission.Id, "Kiểm tra kỹ camera nhiệt tại trụ 15", CancellationToken.None);

        // Assert Add
        log.Should().NotBeNull();
        log.Content.Should().Be("Kiểm tra kỹ camera nhiệt tại trụ 15");
        log.SenderRole.Should().Be("MANAGER");

        notifierMock.Verify(n => n.NotifyAsync(
            It.Is<MissionLifecycleEventDto>(e => e.MissionId == mission.Id.ToString() && e.Type == "COMMUNICATION"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Act - Get communications
        var logs = await service.GetCommunicationsAsync(mission.Id, CancellationToken.None);
        logs.Should().ContainSingle();
        logs[0].Content.Should().Be("Kiểm tra kỹ camera nhiệt tại trụ 15");
    }
}
