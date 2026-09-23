using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.NotificationService.API.Hubs;
using UavPms.NotificationService.API.Jobs;
using UavPms.NotificationService.API.Services;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.NotificationService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Events;
using Xunit;

namespace UavPms.NotificationService.Tests.Features.Notifications;

public class RealtimeMissionNotificationTests
{
    private readonly Mock<IHubContext<NotificationHub>> _hubContextMock;
    private readonly Mock<IHubClients> _clientsMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<INotificationConnectionRegistry> _connectionRegistryMock;
    private readonly Mock<ILogger<RealtimeNotificationService>> _loggerMock;
    private readonly RealtimeNotificationService _service;

    public RealtimeMissionNotificationTests()
    {
        _hubContextMock = new Mock<IHubContext<NotificationHub>>();
        _clientsMock = new Mock<IHubClients>();
        _clientProxyMock = new Mock<IClientProxy>();
        _connectionRegistryMock = new Mock<INotificationConnectionRegistry>();
        _loggerMock = new Mock<ILogger<RealtimeNotificationService>>();

        _hubContextMock.Setup(h => h.Clients).Returns(_clientsMock.Object);
        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);

        _service = new RealtimeNotificationService(
            _hubContextMock.Object,
            _connectionRegistryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SendMissionEventAsync_Confirmed_ShouldBroadcastAggregateAndSpecificEvents()
    {
        // Arrange
        var missionId = Guid.NewGuid().ToString();
        var evt = new MissionLifecycleEventDto
        {
            MissionId = missionId,
            Type = "CONFIRMED",
            Message = "Nhiệm vụ đã được xác nhận"
        };

        // Act
        await _service.SendMissionEventAsync(evt);

        // Assert
        var expectedGroup = $"mission_{missionId}";
        _clientsMock.Verify(c => c.Group(expectedGroup), Times.AtLeastOnce);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "MissionLifecycleEvent",
            It.Is<object[]>(args => args.Length > 0 && args[0] == evt),
            It.IsAny<CancellationToken>()), Times.Once);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "MissionConfirmed",
            It.Is<object[]>(args => args.Length > 0 && args[0] == evt),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMissionEventAsync_Reminder_ShouldBroadcastToMissionGroupAndTargetUser()
    {
        // Arrange
        var missionId = Guid.NewGuid().ToString();
        var targetUserId = Guid.NewGuid();
        var evt = new MissionLifecycleEventDto
        {
            MissionId = missionId,
            Type = "REMINDER",
            TargetUserId = targetUserId.ToString(),
            Message = "Nhắc nhở xác nhận"
        };

        // Act
        await _service.SendMissionEventAsync(evt);

        // Assert
        _clientsMock.Verify(c => c.Group($"mission_{missionId}"), Times.AtLeastOnce);
        _clientsMock.Verify(c => c.Group(NotificationHub.UserGroupName(targetUserId)), Times.Once);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "MissionReminderSent",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SendMissionEventAsync_Overdue_ShouldBroadcastToMissionGroupAndManager()
    {
        // Arrange
        var missionId = Guid.NewGuid().ToString();
        var managerId = Guid.NewGuid();
        var evt = new MissionLifecycleEventDto
        {
            MissionId = missionId,
            Type = "OVERDUE",
            ManagerId = managerId.ToString(),
            Message = "Quá hạn xác nhận"
        };

        // Act
        await _service.SendMissionEventAsync(evt);

        // Assert
        _clientsMock.Verify(c => c.Group($"mission_{missionId}"), Times.AtLeastOnce);
        _clientsMock.Verify(c => c.Group(NotificationHub.UserGroupName(managerId)), Times.Once);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "MissionConfirmationOverdue",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SendMissionEventAsync_Dispatched_ShouldBroadcastToMissionGroupAndInspector()
    {
        // Arrange
        var missionId = Guid.NewGuid().ToString();
        var inspectorId = Guid.NewGuid();
        var evt = new MissionLifecycleEventDto
        {
            MissionId = missionId,
            Type = "DISPATCHED",
            InspectorId = inspectorId.ToString(),
            Message = "Nhiệm vụ đã được giao"
        };

        // Act
        await _service.SendMissionEventAsync(evt);

        // Assert
        _clientsMock.Verify(c => c.Group($"mission_{missionId}"), Times.AtLeastOnce);
        _clientsMock.Verify(c => c.Group(NotificationHub.UserGroupName(inspectorId)), Times.Once);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "MissionDispatched",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task NotificationHub_JoinAndLeaveMissionGroup_ShouldCallGroupsAddAndRemove()
    {
        // Arrange
        var hubLoggerMock = new Mock<ILogger<NotificationHub>>();
        var hub = new NotificationHub(hubLoggerMock.Object, _connectionRegistryMock.Object);
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns("conn-123");

        hub.Groups = mockGroups.Object;
        hub.Context = mockContext.Object;

        var missionId = Guid.NewGuid().ToString();

        // Act
        await hub.JoinMissionGroup(missionId);
        await hub.LeaveMissionGroup(missionId);

        // Assert
        mockGroups.Verify(g => g.AddToGroupAsync("conn-123", $"mission_{missionId}", default), Times.Once);
        mockGroups.Verify(g => g.RemoveFromGroupAsync("conn-123", $"mission_{missionId}", default), Times.Once);
    }

    [Fact]
    public async Task MissionConfirmationOverdueJob_ShouldScanOverdueMissionsAndNotify()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new ApplicationDbContext(options, null);
        var managerId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        var overdueMission = new Mission
        {
            Id = missionId,
            MissionCode = "MS-OVERDUE-01",
            Title = "Khảo sát đường dây quá hạn",
            Status = "PENDING_CONFIRMATION",
            ManagerId = managerId,
            ConfirmationDeadline = DateTime.UtcNow.AddMinutes(-10),
            IsOverdueNotified = false
        };
        db.Missions.Add(overdueMission);
        await db.SaveChangesAsync();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ApplicationDbContext))).Returns(db);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IHubContext<NotificationHub>))).Returns(_hubContextMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var jobLoggerMock = new Mock<ILogger<MissionConfirmationOverdueJob>>();
        var job = new MissionConfirmationOverdueJob(jobLoggerMock.Object, scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        var updatedMission = await db.Missions.FirstAsync(m => m.Id == missionId);
        updatedMission.IsOverdueNotified.Should().BeTrue();

        var notifications = await db.Notifications.ToListAsync();
        notifications.Should().ContainSingle();
        notifications[0].UserId.Should().Be(managerId);
        notifications[0].Type.Should().Be("MISSION_CONFIRMATION_OVERDUE");

        var logs = await db.MissionCommunicationLogs.ToListAsync();
        logs.Should().ContainSingle();
        logs[0].Type.Should().Be("OVERDUE");
    }
}
