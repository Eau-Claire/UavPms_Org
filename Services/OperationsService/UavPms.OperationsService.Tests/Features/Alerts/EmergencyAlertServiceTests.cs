using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Alerts.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Events;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Alerts;

public class EmergencyAlertServiceTests
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
    public async Task GetActiveAlertsAsync_ShouldReturnOnlyActiveAlerts()
    {
        // Arrange
        await using var db = CreateContext();
        var mockPublisher = new Mock<IEventPublisher>();
        var service = new EmergencyAlertService(db, mockPublisher.Object, NullLogger<EmergencyAlertService>.Instance);

        var activeAlert = new EmergencyAlert
        {
            Id = Guid.NewGuid(),
            Status = "Active",
            Priority = "Critical",
            TriggeredAt = DateTime.UtcNow
        };
        var confirmedAlert = new EmergencyAlert
        {
            Id = Guid.NewGuid(),
            Status = "Confirmed",
            Priority = "Critical",
            TriggeredAt = DateTime.UtcNow.AddMinutes(-10)
        };
        db.EmergencyAlerts.AddRange(activeAlert, confirmedAlert);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetActiveAlertsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(activeAlert.Id);
        result[0].Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetAlertHistoryAsync_ShouldSupportStatusFilterAndPagination()
    {
        // Arrange
        await using var db = CreateContext();
        var mockPublisher = new Mock<IEventPublisher>();
        var service = new EmergencyAlertService(db, mockPublisher.Object, NullLogger<EmergencyAlertService>.Instance);

        for (int i = 0; i < 5; i++)
        {
            db.EmergencyAlerts.Add(new EmergencyAlert
            {
                Id = Guid.NewGuid(),
                Status = "Confirmed",
                Priority = "Critical",
                TriggeredAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        db.EmergencyAlerts.Add(new EmergencyAlert
        {
            Id = Guid.NewGuid(),
            Status = "Dismissed",
            Priority = "Low",
            TriggeredAt = DateTime.UtcNow.AddMinutes(-20)
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetAlertHistoryAsync("Confirmed", 1, 3);

        // Assert
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(3);
        result.Items.Should().HaveCount(3);
        result.Items.Should().OnlyContain(x => x.Status == "Confirmed");
    }

    [Fact]
    public async Task ReviewAlertAsync_ShouldUpdateStatusAndReviewNotes()
    {
        // Arrange
        await using var db = CreateContext();
        var mockPublisher = new Mock<IEventPublisher>();
        var service = new EmergencyAlertService(db, mockPublisher.Object, NullLogger<EmergencyAlertService>.Instance);

        var alertId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        db.EmergencyAlerts.Add(new EmergencyAlert
        {
            Id = alertId,
            Status = "Active",
            Priority = "Critical",
            TriggeredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var request = new ReviewEmergencyAlertRequest
        {
            Status = "Confirmed",
            Notes = "Wildfire confirmed near tower base"
        };

        // Act
        var success = await service.ReviewAlertAsync(alertId, reviewerId, request);

        // Assert
        success.Should().BeTrue();
        var updated = await db.EmergencyAlerts.FindAsync(alertId);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("Confirmed");
        updated.ReviewedById.Should().Be(reviewerId);
        updated.Notes.Should().Be("Wildfire confirmed near tower base");
        updated.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EscalateAlertAsync_ShouldInsertEscalationAndPublishNotification()
    {
        // Arrange
        await using var db = CreateContext();
        var mockPublisher = new Mock<IEventPublisher>();
        var service = new EmergencyAlertService(db, mockPublisher.Object, NullLogger<EmergencyAlertService>.Instance);

        var alertId = Guid.NewGuid();
        var analystId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = analystId, FullName = "Analyst John", Email = "analyst@example.com", Status = "Active" },
            new User { Id = managerId, FullName = "Manager Alice", Email = "manager@example.com", Status = "Active" }
        );

        db.EmergencyAlerts.Add(new EmergencyAlert
        {
            Id = alertId,
            Status = "Active",
            Priority = "Critical",
            TriggeredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var request = new EscalateEmergencyAlertRequest
        {
            EscalatedToUserId = managerId,
            Reason = "Power outage danger"
        };

        // Act
        var result = await service.EscalateAlertAsync(alertId, analystId, request);

        // Assert
        result.Should().NotBeNull();
        result.EscalatedBy.Should().Be(analystId);
        result.EscalatedTo.Should().Be(managerId);
        result.Reason.Should().Be("Power outage danger");

        var updatedAlert = await db.EmergencyAlerts.FindAsync(alertId);
        updatedAlert!.Status.Should().Be("Escalated");

        mockPublisher.Verify(p => p.PublishAsync(It.Is<NotificationPushEvent>(e =>
            e.UserId == managerId &&
            e.ReferenceType == "EmergencyAlert" &&
            e.ReferenceId == alertId
        )), Times.Once);
    }

    [Fact]
    public async Task TriggerAlertAsync_ShouldCreateAlertAndNotifyRoles()
    {
        // Arrange
        await using var db = CreateContext();
        var mockPublisher = new Mock<IEventPublisher>();
        var service = new EmergencyAlertService(db, mockPublisher.Object, NullLogger<EmergencyAlertService>.Instance);

        var analystId = Guid.NewGuid();
        var role = new Role { Id = 1, RoleName = "Analyst", Description = "Analyst Role" };
        db.Roles.Add(role);
        db.Users.Add(new User { Id = analystId, FullName = "Analyst Bob", Email = "bob@example.com", Status = "Active" });
        db.UserRoles.Add(new UserRole { UserId = analystId, RoleId = 1, Role = role });
        // Edge alerts without an anomaly create media + Edge AI run + anomaly; media needs a mission.
        var mission = new Mission { Id = Guid.NewGuid(), MissionCode = "MS-EDGE-01", Status = MissionStatus.InProgress };
        db.Missions.Add(mission);
        var tower = new Tower { Id = Guid.NewGuid(), RegionId = Guid.NewGuid(), TowerCode = "TOW-N1-05" };
        db.Towers.Add(tower);
        await db.SaveChangesAsync();

        var request = new TriggerEmergencyAlertRequest
        {
            MissionId = mission.Id,
            TowerCode = "TOW-N1-05",
            AlertType = "Wildfire / Flashover",
            Priority = "Critical",
            DeliveryLatencySeconds = 4
        };

        // Act
        var result = await service.TriggerAlertAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Active");
        result.DeliveryLatencySeconds.Should().Be(4);

        var alert = await db.EmergencyAlerts.Include(a => a.Anomaly!).ThenInclude(a => a.Media).SingleAsync();
        alert.Anomaly!.TowerId.Should().Be(tower.Id);
        alert.Anomaly.Media!.MissionId.Should().Be(mission.Id);
        var run = await db.AIAnalysisRuns.SingleAsync(r => r.Id == alert.Anomaly.AnalysisRunId);
        run.ExecutionLocation.Should().Be(AIAnalysisRun.Edge);
        run.Status.Should().Be(AIAnalysisStatus.Completed);

        mockPublisher.Verify(p => p.PublishAsync(It.Is<NotificationPushEvent>(e =>
            e.UserId == analystId &&
            e.ReferenceType == "EmergencyAlert"
        )), Times.Once);
    }

    [Fact]
    public async Task EscalateAlertAsync_ToSelf_IsRejected()
    {
        await using var db = CreateContext();
        var service = new EmergencyAlertService(db, Mock.Of<IEventPublisher>(), NullLogger<EmergencyAlertService>.Instance);
        var alertId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.EmergencyAlerts.Add(new EmergencyAlert { Id = alertId, Status = "Active", Priority = "Critical", TriggeredAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var act = () => service.EscalateAlertAsync(alertId, userId, new EscalateEmergencyAlertRequest { EscalatedToUserId = userId, Reason = "x" });

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("INVALID_ESCALATION_TARGET");
        (await db.EmergencyAlerts.FindAsync(alertId))!.Status.Should().Be("Active");
        (await db.AlertEscalations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TriggerAlertAsync_WhenAnomalyAlreadyHasActiveAlert_IsRejected()
    {
        await using var db = CreateContext();
        var service = new EmergencyAlertService(db, Mock.Of<IEventPublisher>(), NullLogger<EmergencyAlertService>.Instance);
        var anomalyId = Guid.NewGuid();
        db.EmergencyAlerts.Add(new EmergencyAlert { Id = Guid.NewGuid(), AnomalyId = anomalyId, Status = "Active", Priority = "Critical", TriggeredAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var act = () => service.TriggerAlertAsync(new TriggerEmergencyAlertRequest { AnomalyId = anomalyId, MissionId = Guid.NewGuid() });

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("ALERT_ALREADY_ACTIVE");
        (await db.EmergencyAlerts.CountAsync(a => a.AnomalyId == anomalyId)).Should().Be(1);
    }
}
