using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using System.Net;
using System.Text.Json;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.API.Middlewares;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class MissionStateMachineAndConcurrencyTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId, string role)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        mock.SetupGet(u => u.Username).Returns("Test User");
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

    #region H3: State Machine Tests

    [Fact]
    public async Task SuspendMissionAsync_WhenMissionIsDraft_ShouldThrowInvalidMissionStateException()
    {
        var managerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.SystemAdmin);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MSN-DRAFT",
            Title = "Draft Mission",
            RegionId = regionId,
            ManagerId = managerId,
            Status = MissionStatus.Draft
        };
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        var act = () => service.SuspendMissionAsync(mission.Id, "Weather issue", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INVALID_MISSION_STATE");
    }

    [Fact]
    public async Task ResumeMissionAsync_WhenMissionIsNotSuspended_ShouldThrowInvalidMissionStateException()
    {
        var managerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.SystemAdmin);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MSN-READY",
            Title = "Ready Mission",
            RegionId = regionId,
            ManagerId = managerId,
            Status = MissionStatus.Ready
        };
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        var act = () => service.ResumeMissionAsync(mission.Id, "Clear skies", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INVALID_MISSION_STATE");
    }

    [Fact]
    public async Task CancelMissionAsync_WhenMissionIsPostponed_ShouldSucceedWithout500Error()
    {
        var managerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.SystemAdmin);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MSN-POSTPONED",
            Title = "Postponed Mission",
            RegionId = regionId,
            ManagerId = managerId,
            Status = MissionStatus.Postponed,
            PostponedAt = DateTime.UtcNow
        };
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        var result = await service.CancelMissionAsync(mission.Id, "Cancelled due to rescheduling", CancellationToken.None);

        result.Status.Should().Be(MissionStatus.Cancelled);
        result.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelMissionAsync_WhenMissionIsSuspended_ShouldSucceedWithout500Error()
    {
        var managerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.SystemAdmin);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MSN-SUSPENDED",
            Title = "Suspended Mission",
            RegionId = regionId,
            ManagerId = managerId,
            Status = MissionStatus.Suspended
        };
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        var result = await service.CancelMissionAsync(mission.Id, "Permanent abort after suspension", CancellationToken.None);

        result.Status.Should().Be(MissionStatus.Cancelled);
        result.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecalculateReadiness_WhenMissionIsSuspendedOrPostponed_ShouldNotOverwriteStatus()
    {
        var suspendedMission = new Mission
        {
            Id = Guid.NewGuid(),
            Status = MissionStatus.Suspended,
            UavId = Guid.NewGuid()
        };

        var postponedMission = new Mission
        {
            Id = Guid.NewGuid(),
            Status = MissionStatus.Postponed,
            UavId = Guid.NewGuid()
        };

        suspendedMission.RecalculateReadiness().Should().BeFalse();
        suspendedMission.Status.Should().Be(MissionStatus.Suspended);

        postponedMission.RecalculateReadiness().Should().BeFalse();
        postponedMission.Status.Should().Be(MissionStatus.Postponed);
    }

    #endregion

    #region H6: Concurrency Tests

    [Fact]
    public async Task AcceptAssignmentAsync_ShouldAlwaysIncrementMissionVersion_ToPreventSilentSplitBrain()
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
            Title = "Mission Concurrency Test",
            ManagerId = managerId,
            Status = MissionStatus.PendingAcceptance,
            Version = 5
        };

        var assignment1 = new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = inspectorId,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Pending,
            IsRequired = true,
            Version = 1
        };

        var otherPilotId = Guid.NewGuid();
        var assignment2 = new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = otherPilotId,
            AssignmentRole = "PILOT",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Pending,
            IsRequired = true,
            Version = 1
        };

        mission.Assignments.Add(assignment1);
        mission.Assignments.Add(assignment2);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        // Act: inspector accepts assignment1
        var result = await service.AcceptAssignmentAsync(mission.Id, CancellationToken.None);

        // Assert: mission.Version must have been incremented (from 5 to 6)
        result.ResponseStatus.Should().Be(MissionAssignmentResponse.Accepted);
        var updatedMission = await db.Missions.FindAsync(mission.Id);
        updatedMission!.Version.Should().Be(6);
        // And mission should still be PendingAcceptance because assignment2 has not accepted yet
        updatedMission.Status.Should().Be(MissionStatus.PendingAcceptance);
    }

    #endregion

    #region M2: GlobalExceptionHandler Tests

    [Fact]
    public async Task GlobalExceptionHandler_WhenBusinessRuleExceptionWithInvalidMissionState_ShouldReturnHttp409Conflict()
    {
        var loggerMock = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = new BusinessRuleException("INVALID_MISSION_STATE", "Cannot postpone mission with status InProgress.");

        // Act
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        responseBody.Should().Contain("INVALID_MISSION_STATE");
        responseBody.Should().Contain("Cannot postpone mission with status InProgress.");
    }

    [Fact]
    public async Task GlobalExceptionHandler_WhenDbUpdateConcurrencyException_ShouldReturnHttp409Conflict()
    {
        var loggerMock = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = new DbUpdateConcurrencyException("Concurrency conflict");

        // Act
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        responseBody.Should().Contain("CONCURRENCY_CONFLICT");
    }

    #endregion
}
