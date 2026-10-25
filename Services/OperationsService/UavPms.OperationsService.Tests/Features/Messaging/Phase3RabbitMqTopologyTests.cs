using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Features.Maintenance.Commands.CreateMaintenanceTicket;
using UavPms.OperationsService.Application.Features.Missions.Commands.DispatchMission;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Authorization;
using UavPms.OperationsService.Infrastructure.Messaging;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Messaging;

public class Phase3RabbitMqTopologyTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { UserRoles.SystemAdmin });
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
    public void EventTopologyConstants_TopologyValues_MustMatchPhase3Requirements()
    {
        EventTopologyConstants.Exchange.Should().Be("uavpms.events");

        EventTopologyConstants.RoutingKeys.InspectionMediaUploaded.Should().Be("inspection.media.uploaded");
        EventTopologyConstants.RoutingKeys.AiDetectionCompleted.Should().Be("ai.detection.completed");
        EventTopologyConstants.RoutingKeys.MissionDispatched.Should().Be("mission.dispatched");
        EventTopologyConstants.RoutingKeys.MaintenanceTicketAssigned.Should().Be("maintenance.ticket.assigned");

        EventTopologyConstants.Queues.AiInspectionMediaProcess.Should().Be("ai-inspection.media-process");
        EventTopologyConstants.Queues.OperationsDefectRecord.Should().Be("operations.defect-record");
        EventTopologyConstants.Queues.NotificationsDefectAlert.Should().Be("notifications.defect-alert");
        EventTopologyConstants.Queues.NotificationsMissionDispatch.Should().Be("notifications.mission-dispatch");
        EventTopologyConstants.Queues.NotificationsTicketAssign.Should().Be("notifications.ticket-assign");
    }

    [Theory]
    [InlineData(typeof(InspectionMediaUploadedEvent), "inspection.media.uploaded")]
    [InlineData(typeof(MissionDispatchedEvent), "mission.dispatched")]
    [InlineData(typeof(MaintenanceTicketAssignedEvent), "maintenance.ticket.assigned")]
    [InlineData(typeof(AiDetectionCompletedEvent), "ai.detection.completed")]
    public void OperationsService_RabbitMqEventPublisher_ShouldProduceCorrectRoutingKey(Type eventType, string expectedRoutingKey)
    {
        var method = typeof(RabbitMqEventPublisher).GetMethod(
            "GetRoutingKey",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var eventInstance = Activator.CreateInstance(eventType)!;

        var routingKey = method!
            .MakeGenericMethod(eventType)
            .Invoke(null, new object[] { eventType.Name, eventInstance });

        routingKey.Should().Be(expectedRoutingKey);
    }

    [Fact]
    public async Task DispatchMissionCommandHandler_PersistsMissionDispatchedEvent_InOutbox()
    {
        var managerId = Guid.NewGuid();
        var pilotId = Guid.NewGuid();
        var droneId = Guid.NewGuid();
        var user = CreateUserMock(managerId);
        await using var db = CreateContext(user.Object);

        var drone = new Uav
        {
            Id = droneId,
            UavCode = "UAV-DISPATCH-01",
            OperationalStatus = DroneOperationalStatus.Available
        };
        db.Uavs.Add(drone);

        var inspection = new DroneTechnicalInspection
        {
            Id = Guid.NewGuid(),
            DroneId = droneId,
            Status = DroneTechnicalInspectionStatus.Passed,
            Health = TechnicalHealth.Healthy,
            ValidUntil = DateTime.UtcNow.AddDays(30)
        };
        db.DroneTechnicalInspections.Add(inspection);

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MSN-DISP-01",
            Title = "Dispatch Mission Test",
            Status = MissionStatus.Ready,
            ManagerId = managerId,
            UavId = droneId,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(2),
            MissionTargets = new List<MissionTarget>
            {
                new() { AssetId = Guid.NewGuid() }
            },
            Assignments = new List<MissionAssignment>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    UserId = pilotId,
                    AssignmentRole = "Pilot",
                    IsRequired = true,
                    Status = MissionAssignmentStatus.Active,
                    ResponseStatus = MissionAssignmentResponse.Accepted
                }
            }
        };
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var missionRepo = new MissionRepository(db);
        var uavRepo = new UavRepository(db);
        var inspectionRepo = new GenericRepository<DroneTechnicalInspection>(db);
        var bookingRepo = new GenericRepository<ResourceBooking>(db);
        var outboxRepo = new GenericRepository<OutboxMessage>(db);
        var uow = new UnitOfWork(db);

        var handler = new DispatchMissionCommandHandler(
            missionRepo, uavRepo, inspectionRepo, bookingRepo, uow, notifier: null, outboxRepository: outboxRepo);

        var command = new DispatchMissionCommand(mission.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be("Assigned");

        var outboxMessages = await db.OutboxMessages.ToListAsync();
        outboxMessages.Should().ContainSingle(m => m.MessageType == nameof(MissionDispatchedEvent));

        var outboxMsg = outboxMessages.First(m => m.MessageType == nameof(MissionDispatchedEvent));
        outboxMsg.Payload.Should().Contain("MSN-DISP-01");
        outboxMsg.Payload.Should().Contain(pilotId.ToString());
    }

    [Fact]
    public async Task CreateMaintenanceTicketCommandHandler_PersistsMaintenanceTicketAssignedEvent_InOutbox()
    {
        var managerId = Guid.NewGuid();
        var techId = Guid.NewGuid();
        var user = CreateUserMock(managerId);
        await using var db = CreateContext(user.Object);

        var tech = new User
        {
            Id = techId,
            FullName = "Technician One",
            Email = "tech@example.com",
            Status = "Active"
        };
        db.Users.Add(tech);

        var anomaly = new DetectedAnomaly
        {
            Id = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            TowerId = Guid.NewGuid().ToString(),
            ValidationStatus = "Confirmed"
        };
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        var accessFilter = new GeographicAccessFilter(db, user.Object);
        var ticketRepo = new GenericRepository<MaintenanceTicket>(db);
        var towerRepo = new TowerRepository(db, accessFilter);
        var anomalyRepo = new AnomalyRepository(db);
        var userRepo = new UserRepository(db);
        var outboxRepo = new GenericRepository<OutboxMessage>(db);
        var uow = new UnitOfWork(db);

        var handler = new CreateMaintenanceTicketCommandHandler(
            ticketRepo, towerRepo, anomalyRepo, userRepo, user.Object, uow, outboxRepo);

        var command = new CreateMaintenanceTicketCommand(
            AnomalyId: anomaly.Id,
            TechnicianId: techId,
            Description: "Repair cracked insulator on phase A",
            Priority: "High");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.TechnicianId.Should().Be(techId);

        var outboxMessages = await db.OutboxMessages.ToListAsync();
        outboxMessages.Should().ContainSingle(m => m.MessageType == nameof(MaintenanceTicketAssignedEvent));

        var outboxMsg = outboxMessages.First(m => m.MessageType == nameof(MaintenanceTicketAssignedEvent));
        outboxMsg.Payload.Should().Contain(techId.ToString());
        outboxMsg.Payload.Should().Contain("Repair cracked insulator");
    }
}
