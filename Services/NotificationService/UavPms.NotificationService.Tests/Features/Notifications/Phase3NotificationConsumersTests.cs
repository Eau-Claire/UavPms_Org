using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.NotificationService.Application.Features.Notifications.Commands.CreateNotification;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Domain.Interfaces.Repositories;
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.NotificationService.Infrastructure.Messaging;
using UavPms.Shared.Contracts.Events;
using Xunit;

namespace UavPms.NotificationService.Tests.Features.Notifications;

public class Phase3NotificationConsumersTests
{
    [Fact]
    public void EventTopologyConstants_ShouldMatchPhase3Spec()
    {
        EventTopologyConstants.Exchange.Should().Be("uavpms.events");
        EventTopologyConstants.RoutingKeys.AiDetectionCompleted.Should().Be("ai.detection.completed");
        EventTopologyConstants.RoutingKeys.MissionDispatched.Should().Be("mission.dispatched");
        EventTopologyConstants.RoutingKeys.MaintenanceTicketAssigned.Should().Be("maintenance.ticket.assigned");
        EventTopologyConstants.RoutingKeys.InspectionMediaUploaded.Should().Be("inspection.media.uploaded");

        EventTopologyConstants.Queues.NotificationsDefectAlert.Should().Be("notifications.defect-alert");
        EventTopologyConstants.Queues.NotificationsMissionDispatch.Should().Be("notifications.mission-dispatch");
        EventTopologyConstants.Queues.NotificationsTicketAssign.Should().Be("notifications.ticket-assign");
    }

    [Fact]
    public async Task MissionDispatchedNotification_CreatesNotificationForAssignedPilotsAndManager()
    {
        var pilotId1 = Guid.NewGuid();
        var pilotId2 = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        var dispatchedEvent = new MissionDispatchedEvent
        {
            EventId = Guid.NewGuid(),
            MissionId = missionId,
            MissionCode = "MSN-2026-001",
            Title = "Transmission Line 500kV Inspection",
            ManagerId = managerId,
            AssignedPilotIds = new List<Guid> { pilotId1, pilotId2 },
            DispatchedAt = DateTime.UtcNow
        };

        var notificationRepoMock = new Mock<INotificationRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var realtimeMock = new Mock<IRealtimeNotificationService>();

        var handler = new CreateNotificationCommandHandler(
            notificationRepoMock.Object,
            unitOfWorkMock.Object,
            realtimeMock.Object);

        // Simulate pilot 1 notification
        var command1 = new CreateNotificationCommand(
            pilotId1,
            "MissionDispatched",
            "Mission",
            dispatchedEvent.MissionId,
            "Nhiệm vụ đã được điều phối thực hiện",
            $"Nhiệm vụ '{dispatchedEvent.MissionCode}' ({dispatchedEvent.Title}) đã sẵn sàng cất cánh.");

        var res1 = await handler.Handle(command1, CancellationToken.None);
        res1.UserId.Should().Be(pilotId1);
        res1.Type.Should().Be("MissionDispatched");

        // Simulate manager notification
        var commandManager = new CreateNotificationCommand(
            managerId,
            "MissionDispatched",
            "Mission",
            dispatchedEvent.MissionId,
            "Điều phối nhiệm vụ thành công",
            $"Nhiệm vụ '{dispatchedEvent.MissionCode}' đã được điều phối thành công sang trạng thái thực địa.");

        var resManager = await handler.Handle(commandManager, CancellationToken.None);
        resManager.UserId.Should().Be(managerId);
    }

    [Fact]
    public async Task MaintenanceTicketAssignedNotification_CreatesNotificationForTechnician()
    {
        var techId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        var ticketEvent = new MaintenanceTicketAssignedEvent
        {
            EventId = Guid.NewGuid(),
            TicketId = ticketId,
            TicketCode = "TKT-2026-0099",
            Title = "Repair Damaged Insulator",
            TowerId = Guid.NewGuid(),
            AssignedToUserId = techId,
            Priority = "Emergency",
            AnomalyDescription = "Cracked insulator bell with imminent flashover risk",
            AssignedAt = DateTime.UtcNow
        };

        var notificationRepoMock = new Mock<INotificationRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var realtimeMock = new Mock<IRealtimeNotificationService>();

        var handler = new CreateNotificationCommandHandler(
            notificationRepoMock.Object,
            unitOfWorkMock.Object,
            realtimeMock.Object);

        var command = new CreateNotificationCommand(
            ticketEvent.AssignedToUserId,
            "TicketAssigned",
            "MaintenanceTicket",
            ticketEvent.TicketId,
            $"Phiếu bảo dưỡng mới: {ticketEvent.TicketCode}",
            $"Bạn đã được phân công xử lý phiếu bảo dưỡng '{ticketEvent.TicketCode}'. Mức độ ưu tiên: {ticketEvent.Priority}. {ticketEvent.AnomalyDescription}");

        var res = await handler.Handle(command, CancellationToken.None);
        res.UserId.Should().Be(techId);
        res.Type.Should().Be("TicketAssigned");
        res.Title.Should().Contain("TKT-2026-0099");
    }
}
