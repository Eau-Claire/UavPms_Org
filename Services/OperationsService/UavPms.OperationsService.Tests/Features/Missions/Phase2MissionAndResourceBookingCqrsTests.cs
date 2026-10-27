using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Application.Features.Missions.Commands.AssignMissionMember;
using UavPms.OperationsService.Application.Features.Missions.Commands.DispatchMission;
using UavPms.OperationsService.Application.Features.Missions.Commands.LockResourceBooking;
using UavPms.OperationsService.Application.Features.Missions.Commands.RespondAssignment;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class Phase2MissionAndResourceBookingCqrsTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        return mock;
    }

    private static ApplicationDbContext CreateContext()
    {
        var user = CreateUserMock(Guid.NewGuid());
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options, user.Object);
    }

    [Fact]
    public async Task LockResourceBooking_SuccessWhenNoConflict()
    {
        await using var db = CreateContext();
        var bookingRepo = new GenericRepository<ResourceBooking>(db);
        var uow = new UnitOfWork(db);
        var handler = new LockResourceBookingCommandHandler(bookingRepo, uow);

        var missionId = Guid.NewGuid();
        var pilotId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(2);
        var end = start.AddHours(2);

        var command = new LockResourceBookingCommand(missionId, pilotId, null, start, end);
        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.MissionId.Should().Be(missionId);
        result.UserId.Should().Be(pilotId);
        result.Status.Should().Be("Active");
    }

    [Fact]
    public async Task LockResourceBooking_ThrowsConflictWhenOverlapDetected()
    {
        await using var db = CreateContext();
        var bookingRepo = new GenericRepository<ResourceBooking>(db);
        var uow = new UnitOfWork(db);

        var pilotId = Guid.NewGuid();
        var existingStart = DateTime.UtcNow.AddHours(2);
        var existingEnd = existingStart.AddHours(2);

        db.ResourceBookings.Add(new ResourceBooking
        {
            Id = Guid.NewGuid(),
            MissionId = Guid.NewGuid(),
            UserId = pilotId,
            StartAt = existingStart,
            EndAt = existingEnd,
            Status = ResourceBookingStatus.Active
        });
        await db.SaveChangesAsync();

        var handler = new LockResourceBookingCommandHandler(bookingRepo, uow);

        // Overlapping window: starts inside existing booking
        var newStart = existingStart.AddMinutes(30);
        var newEnd = newStart.AddHours(2);
        var command = new LockResourceBookingCommand(Guid.NewGuid(), pilotId, null, newStart, newEnd);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*RESOURCE_BOOKING_CONFLICT*");
    }

    [Fact]
    public async Task DispatchMission_ThrowsWhenNoTargets()
    {
        await using var db = CreateContext();
        var missionRepo = new Mock<IMissionRepository>();
        var uavRepo = new Mock<IUavRepository>();
        var inspectionRepo = new Mock<IGenericRepository<DroneTechnicalInspection>>();
        var bookingRepo = new Mock<IGenericRepository<ResourceBooking>>();
        var uow = new Mock<IUnitOfWork>();

        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionTargets = new List<MissionTarget>() // Empty targets
        };

        missionRepo.Setup(r => r.GetByIdAsync(missionId, true)).ReturnsAsync(mission);

        var handler = new DispatchMissionCommandHandler(
            missionRepo.Object, uavRepo.Object, inspectionRepo.Object, bookingRepo.Object, uow.Object);

        var command = new DispatchMissionCommand(missionId);
        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*MISSION_TARGETS_REQUIRED*");
    }

    [Fact]
    public async Task DispatchMission_ThrowsWhenRequiredMembersNotAccepted()
    {
        await using var db = CreateContext();
        var missionRepo = new Mock<IMissionRepository>();
        var uavRepo = new Mock<IUavRepository>();
        var inspectionRepo = new Mock<IGenericRepository<DroneTechnicalInspection>>();
        var bookingRepo = new Mock<IGenericRepository<ResourceBooking>>();
        var uow = new Mock<IUnitOfWork>();

        var missionId = Guid.NewGuid();
        var droneId = Guid.NewGuid();
        var pilotId = Guid.NewGuid();

        var mission = new Mission
        {
            Id = missionId,
            UavId = droneId,
            MissionTargets = new List<MissionTarget> { new() { TowerId = Guid.NewGuid() } },
            Assignments = new List<MissionAssignment>
            {
                new()
                {
                    UserId = pilotId,
                    IsRequired = true,
                    Status = MissionAssignmentStatus.Active,
                    ResponseStatus = MissionAssignmentResponse.Pending // Not accepted
                }
            }
        };

        var drone = new Uav { Id = droneId, TechnicalHealth = TechnicalHealth.Healthy };

        missionRepo.Setup(r => r.GetByIdAsync(missionId, true)).ReturnsAsync(mission);
        uavRepo.Setup(r => r.GetByIdAsync(droneId, true)).ReturnsAsync(drone);
        inspectionRepo.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<DroneTechnicalInspection, bool>>>(), false))
            .ReturnsAsync(new List<DroneTechnicalInspection>
            {
                new() { DroneId = droneId, Status = DroneTechnicalInspectionStatus.Passed, ValidUntil = DateTime.UtcNow.AddDays(10) }
            });

        var handler = new DispatchMissionCommandHandler(
            missionRepo.Object, uavRepo.Object, inspectionRepo.Object, bookingRepo.Object, uow.Object);

        var command = new DispatchMissionCommand(missionId);
        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*PERSONNEL_NOT_ACCEPTED*");
    }

    [Fact]
    public async Task AssignMissionMember_DelegatesToLifecycleService()
    {
        var lifecycle = new Mock<IMissionLifecycleService>();
        var missionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var assignment = new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            UserId = userId,
            AssignmentRole = "Pilot",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Pending
        };

        lifecycle.Setup(l => l.AssignAsync(missionId, It.IsAny<Mf01Assignment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignment);

        var handler = new AssignMissionMemberCommandHandler(lifecycle.Object);
        var command = new AssignMissionMemberCommand(missionId, userId, "Pilot");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.AssignmentRole.Should().Be("Pilot");
    }

    [Fact]
    public async Task RespondAssignment_DelegatesAcceptToLifecycleService()
    {
        var lifecycle = new Mock<IMissionLifecycleService>();
        var missionId = Guid.NewGuid();

        var assignment = new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            ResponseStatus = MissionAssignmentResponse.Accepted
        };

        lifecycle.Setup(l => l.AcceptAssignmentAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignment);

        var handler = new RespondAssignmentCommandHandler(lifecycle.Object);
        var command = new RespondAssignmentCommand(missionId, true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.ResponseStatus.Should().Be("Accepted");
    }
}
