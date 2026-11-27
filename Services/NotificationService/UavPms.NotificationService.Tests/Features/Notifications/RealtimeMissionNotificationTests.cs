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
using System.Security.Claims;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.NotificationService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;
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
        _clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(_clientProxyMock.Object);
        _connectionRegistryMock.Setup(r => r.GetConnections(It.IsAny<string>())).Returns(["conn-target"]);

        _service = new RealtimeNotificationService(
            _hubContextMock.Object,
            _connectionRegistryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    [Trait("Gate5", "P0-3")]
    public async Task SendMissionEventAsync_Confirmed_ShouldBroadcastAggregateAndSpecificEvents()
    {
        // Arrange
        var missionId = Guid.NewGuid().ToString();
        var evt = new MissionLifecycleEventDto
        {
            MissionId = missionId,
            Type = "CONFIRMED",
            TargetUserId = Guid.NewGuid().ToString(),
            Message = "Nhiệm vụ đã được xác nhận"
        };

        // Act
        await _service.SendMissionEventAsync(evt);

        // Assert
        var expectedGroup = $"mission_{missionId}";
        _connectionRegistryMock.Verify(r => r.GetConnections(expectedGroup), Times.Once);
        _clientsMock.Verify(c => c.Clients(It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "conn-target" }))), Times.Once);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "MissionLifecycleEvent",
            It.Is<object[]>(args => args.Length > 0 && args[0] == evt),
            It.IsAny<CancellationToken>()), Times.Once);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Gate5", "P0-3")]
    public void NotificationHub_ShouldNotExposeClientCallableSendMethods()
    {
        var methods = typeof(NotificationHub).GetMethods(System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

        methods.Should().NotContain(method => method.Name.StartsWith("Broadcast", StringComparison.Ordinal)
            || method.Name.StartsWith("Send", StringComparison.Ordinal));
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
        _connectionRegistryMock.Verify(r => r.GetConnections(NotificationHub.UserGroupName(targetUserId)), Times.Once);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "MissionLifecycleEvent",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
        _connectionRegistryMock.Verify(r => r.GetConnections(NotificationHub.UserGroupName(managerId)), Times.Once);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "MissionLifecycleEvent",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
        _connectionRegistryMock.Verify(r => r.GetConnections(NotificationHub.UserGroupName(inspectorId)), Times.Once);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "MissionLifecycleEvent",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
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

        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, UserRoles.SystemAdmin)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        mockContext.Setup(c => c.User).Returns(claimsPrincipal);

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
    [Trait("Gate5", "P0-3")]
    public async Task NotificationHub_JoinMissionGroup_WhenUnauthenticated_ShouldReject()
    {
        // Arrange
        var hubLoggerMock = new Mock<ILogger<NotificationHub>>();
        var hub = new NotificationHub(hubLoggerMock.Object, _connectionRegistryMock.Object);
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns("conn-123");
        mockContext.Setup(c => c.User).Returns((ClaimsPrincipal?)null);

        hub.Groups = mockGroups.Object;
        hub.Context = mockContext.Object;

        var missionId = Guid.NewGuid().ToString();

        // Act
        await Assert.ThrowsAsync<HubException>(() => hub.JoinMissionGroup(missionId));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRoles.Analyst)
        };
        mockContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));
        await Assert.ThrowsAsync<HubException>(() => hub.JoinAssessmentGroup(Guid.NewGuid().ToString()));

        // Assert
        mockGroups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Theory]
    [InlineData(UserRoles.Analyst)]
    [InlineData(UserRoles.Technician)]
    [Trait("Gate5", "P0-3")]
    public async Task JoinMissionGroup_WithoutMissionRole_ShouldRejectBeforeCheckingData(string role)
    {
        var registry = new NotificationConnectionRegistry();
        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        using var hub = CreateHub(role, registry, out var groups, scopeFactory.Object);
        var missionId = Guid.NewGuid().ToString("D");

        await Assert.ThrowsAsync<HubException>(() => hub.JoinMissionGroup(missionId));

        scopeFactory.VerifyNoOtherCalls();
        groups.VerifyNoOtherCalls();
        Assert.Empty(registry.GetConnections(NotificationHub.MissionGroupName(missionId)));
    }

    [Theory]
    [InlineData(UserRoles.Inspector, true)]
    [InlineData(UserRoles.Analyst, false)]
    [InlineData(UserRoles.Technician, false)]
    [Trait("Gate5", "P0-3")]
    public async Task JoinAssessmentGroups_ShouldEnforceRole(string role, bool allowed)
    {
        var registry = new NotificationConnectionRegistry();
        using var hub = CreateHub(role, registry, out var groups);
        var assessmentId = Guid.NewGuid().ToString("D");
        var assessmentGroup = NotificationHub.AssessmentGroupName(assessmentId);
        var listGroup = NotificationHub.AssessmentsListGroupName;

        if (!allowed)
        {
            await Assert.ThrowsAsync<HubException>(() => hub.JoinAssessmentGroup(assessmentId));
            await Assert.ThrowsAsync<HubException>(() => hub.JoinAssessmentsListGroup());
            groups.VerifyNoOtherCalls();
            Assert.Empty(registry.GetConnections(assessmentGroup));
            Assert.Empty(registry.GetConnections(listGroup));
            return;
        }

        await hub.JoinAssessmentGroup(assessmentId);
        await hub.JoinAssessmentsListGroup();
        Assert.Contains(hub.Context.ConnectionId, registry.GetConnections(assessmentGroup));
        Assert.Contains(hub.Context.ConnectionId, registry.GetConnections(listGroup));
    }

    [Fact]
    [Trait("Gate5", "P0-3")]
    public async Task LeaveAndDisconnect_ShouldRemoveOnlyCurrentConnectionFromGroups()
    {
        var registry = new NotificationConnectionRegistry();
        using var hub = CreateHub(UserRoles.SystemAdmin, registry, out var groups);
        var missionId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var missionGroup = NotificationHub.MissionGroupName(missionId.ToString("D"));
        var assessmentGroup = NotificationHub.AssessmentGroupName(assessmentId.ToString("D"));
        var listGroup = NotificationHub.AssessmentsListGroupName;
        var connectionId = hub.Context.ConnectionId;
        const string otherConnectionId = "other-connection";

        await hub.JoinMissionGroup(missionId.ToString("D"));
        await hub.JoinAssessmentGroup(assessmentId.ToString("D"));
        await hub.JoinAssessmentsListGroup();
        await hub.LeaveMissionGroup(missionId.ToString("B").ToUpperInvariant());
        await hub.LeaveAssessmentGroup(assessmentId.ToString("B").ToUpperInvariant());
        groups.Verify(g => g.RemoveFromGroupAsync(connectionId, missionGroup, default), Times.Once);
        groups.Verify(g => g.RemoveFromGroupAsync(connectionId, assessmentGroup, default), Times.Once);
        Assert.Empty(registry.GetConnections(missionGroup));
        Assert.Empty(registry.GetConnections(assessmentGroup));

        await hub.JoinMissionGroup(missionId.ToString("D"));
        await hub.JoinAssessmentGroup(assessmentId.ToString("D"));
        registry.AddToGroup(assessmentGroup, otherConnectionId);

        await hub.OnDisconnectedAsync(null);

        Assert.Empty(registry.GetConnections(missionGroup));
        Assert.Empty(registry.GetConnections(listGroup));
        Assert.Equal(new[] { otherConnectionId }, registry.GetConnections(assessmentGroup));
    }

    private static NotificationHub CreateHub(
        string role, NotificationConnectionRegistry registry, out Mock<IGroupManager> groups,
        IServiceScopeFactory? scopeFactory = null)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
            new Claim(ClaimTypes.Role, role)
        };
        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns("test-connection");
        context.SetupGet(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));
        groups = new Mock<IGroupManager>();
        return new NotificationHub(new Mock<ILogger<NotificationHub>>().Object, registry, scopeFactory)
        {
            Context = context.Object,
            Groups = groups.Object
        };
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
            Status = "PendingAcceptance",
            ManagerId = managerId,
            ConfirmationDeadline = DateTime.UtcNow.AddMinutes(-10),
            OverdueNotifiedAt = null
        };
        db.Missions.Add(overdueMission);
        var inspectorId = Guid.NewGuid();
        db.MissionAssignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            UserId = inspectorId,
            AssignmentRole = "INSPECTOR",
            Status = "Active"
        });
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
        updatedMission.OverdueNotifiedAt.Should().NotBeNull();

        var notifications = await db.Notifications.ToListAsync();
        notifications.Should().ContainSingle();
        notifications[0].UserId.Should().Be(managerId);
        notifications[0].Type.Should().Be("MISSION_CONFIRMATION_OVERDUE");

        var logs = await db.MissionCommunicationLogs.ToListAsync();
        logs.Should().ContainSingle();
        logs[0].Type.Should().Be("OVERDUE");
        logs[0].SenderId.Should().BeNull();

        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "MissionLifecycleEvent",
            It.Is<object[]>(args => args.Length == 1
                && args[0] is MissionLifecycleEventDto
                && ((MissionLifecycleEventDto)args[0]).InspectorId == inspectorId.ToString()
                && ((MissionLifecycleEventDto)args[0]).Log!.SenderName == "Hệ Thống"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
