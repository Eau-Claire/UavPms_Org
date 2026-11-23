using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UavPms.NotificationService.API.Hubs;
using UavPms.NotificationService.API.Jobs;
using UavPms.NotificationService.API.Services;
using UavPms.NotificationService.Application.Features.Notifications.Commands.CreateNotification;
using UavPms.NotificationService.Domain.Contracts;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.NotificationService.Infrastructure.Persistence;
using UavPms.NotificationService.Infrastructure.Repositories;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;
using Xunit;

namespace UavPms.NotificationService.Tests.Features.Notifications;

/// <summary>
/// Bottom-Up Modular Integration Tests for NotificationService:
/// Combines CreateNotificationCommandHandler + RealtimeNotificationService (SignalR Hub group routing)
/// + CleanupJob with configured Notification:RetentionDays against a shared EF Core database context.
/// </summary>
public class NotificationBottomUpModularIntegrationTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"notif-bottom-up-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, null);
    }

    [Fact]
    public async Task CreateNotification_RealtimeSignalRGroupRouting_AndCleanupJobWithConfiguredRetentionDays_ShouldWorkEndToEnd()
    {
        await using var db = CreateContext();

        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = managerId, Email = "manager@uavpms.vn", FullName = "Trần Quản Lý" },
            new User { Id = inspectorId, Email = "inspector@uavpms.vn", FullName = "Lê Kiểm Tra" });
        await db.SaveChangesAsync();

        // 1. Wire SignalR HubContext + ConnectionRegistry + RealtimeNotificationService + CreateNotificationCommandHandler
        var hubContextMock = new Mock<IHubContext<NotificationHub>>();
        var hubClientsMock = new Mock<IHubClients>();
        var groupTargets = new Dictionary<string, Mock<IClientProxy>>();

        hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);
        hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns((string groupName) =>
        {
            if (!groupTargets.TryGetValue(groupName, out var proxyMock))
            {
                proxyMock = new Mock<IClientProxy>();
                groupTargets[groupName] = proxyMock;
            }
            return proxyMock.Object;
        });

        var connectionRegistry = new Mock<INotificationConnectionRegistry>();
        connectionRegistry.Setup(r => r.GetConnections(It.IsAny<string>())).Returns(new[] { "conn-1" });

        var realtimeService = new RealtimeNotificationService(
            hubContextMock.Object,
            connectionRegistry.Object,
            NullLogger<RealtimeNotificationService>.Instance);

        var notifRepo = new NotificationRepository(db);
        var uow = new UnitOfWork(db);
        var createHandler = new CreateNotificationCommandHandler(notifRepo, uow, realtimeService);

        // 2. Create emergency notification -> persisted in DB and pushed to user:{managerId} SignalR group
        var createdDto = await createHandler.Handle(new CreateNotificationCommand(
            UserId: managerId,
            Type: "EmergencyAlert",
            Title: "Cảnh báo phóng điện khẩn cấp",
            Body: "Phát hiện phóng điện bề mặt cách điện tại cột T-500KV-01",
            ReferenceType: "EmergencyAlert",
            ReferenceId: missionId), CancellationToken.None);

        createdDto.UserId.Should().Be(managerId);
        var managerGroup = NotificationHub.UserGroupName(managerId);
        groupTargets.Should().ContainKey(managerGroup);
        groupTargets[managerGroup].Verify(
            p => p.SendCoreAsync(
                RealtimeNotificationEvents.NotificationReceived,
                It.Is<object?[]>(args => args.Length == 1 && args[0] != null),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // 3. Broadcast Mission DISPATCHED lifecycle event -> routes to mission:{missionId} AND user:{inspectorId}
        await realtimeService.SendMissionEventAsync(new MissionLifecycleEventDto
        {
            MissionId = missionId.ToString(),
            Type = "DISPATCHED",
            InspectorId = inspectorId.ToString(),
            Message = "Mission dispatched to field inspector"
        }, CancellationToken.None);

        var missionGroup = NotificationHub.MissionGroupName(missionId.ToString());
        var inspectorGroup = NotificationHub.UserGroupName(inspectorId);
        groupTargets.Should().ContainKey(missionGroup);
        groupTargets.Should().ContainKey(inspectorGroup);

        groupTargets[missionGroup].Verify(
            p => p.SendCoreAsync("MissionDispatched", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
        groupTargets[inspectorGroup].Verify(
            p => p.SendCoreAsync("MissionDispatched", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // 4. Seed historical RefreshTokens and run CleanupJob with custom Notification:RetentionDays = 14
        db.RefreshTokens.AddRange(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = managerId,
                TokenHash = "revoked-20-days-ago",
                ExpiresAt = DateTime.UtcNow.AddDays(10),
                RevokedAt = DateTime.UtcNow.AddDays(-20),
                CreatedAt = DateTime.UtcNow.AddDays(-25)
            },
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = managerId,
                TokenHash = "revoked-7-days-ago",
                ExpiresAt = DateTime.UtcNow.AddDays(10),
                RevokedAt = DateTime.UtcNow.AddDays(-7),
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notification:RetentionDays"] = "14",
                ["FileStorage:AlertImagesPath"] = string.Empty
            })
            .Build();

        var scopeMock = new Mock<IServiceScope>();
        var spMock = new Mock<IServiceProvider>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        spMock.Setup(sp => sp.GetService(typeof(ApplicationDbContext))).Returns(db);
        scopeMock.SetupGet(s => s.ServiceProvider).Returns(spMock.Object);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var cleanupJob = new CleanupJob(NullLogger<CleanupJob>.Instance, config, scopeFactoryMock.Object);
        await cleanupJob.Execute();

        // Token revoked 20 days ago (> 14 days retention) is purged; token revoked 7 days ago (< 14 days) is kept
        var remainingHashes = await db.RefreshTokens.Select(t => t.TokenHash).ToListAsync();
        remainingHashes.Should().Equal("revoked-7-days-ago");
    }
}
