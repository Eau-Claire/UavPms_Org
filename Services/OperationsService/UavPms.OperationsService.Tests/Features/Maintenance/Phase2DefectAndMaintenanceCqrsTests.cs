using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Alerts.Commands.TriggerEmergencyAlert;
using UavPms.OperationsService.Application.Features.Maintenance.Commands.CompleteMaintenanceTicket;
using UavPms.OperationsService.Application.Features.Maintenance.Commands.CreateMaintenanceTicket;
using UavPms.OperationsService.Application.Features.Maintenance.Commands.ReviewDetectedAnomaly;
using UavPms.OperationsService.Application.Features.Maintenance.Commands.UploadMaintenanceProof;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Authorization;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Maintenance;

public class Phase2DefectAndMaintenanceCqrsTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { UavPms.Shared.Contracts.Constants.UserRoles.SystemAdmin });
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

    private static ReviewDetectedAnomalyCommandHandler CreateReviewHandler(ApplicationDbContext db, ICurrentUserServices user)
    {
        var accessFilter = new GeographicAccessFilter(db, user);
        return new ReviewDetectedAnomalyCommandHandler(
            new AnomalyRepository(db),
            new MissionRepository(db),
            new TowerRepository(db, accessFilter),
            new GenericRepository<EmergencyAlert>(db),
            new GenericRepository<MaintenanceTicket>(db),
            user,
            new UnitOfWork(db));
    }

    [Fact]
    public async Task ReviewDetectedAnomaly_StoresAnalystFeedbackAndEscalatesEmergency()
    {
        var analystId = Guid.NewGuid();
        var userMock = CreateUserMock(analystId);
        await using var db = CreateContext(userMock.Object);

        var towerId = Guid.NewGuid();
        db.Towers.Add(new Tower { Id = towerId, RegionId = Guid.NewGuid(), TowerCode = "T-100" });

        var missionId = Guid.NewGuid();
        var anomaly = new DetectedAnomaly
        {
            Id = Guid.NewGuid(),
            TowerId = towerId,
            ConfidenceScore = 0.95,
            ValidationStatus = "Pending"
        };
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        var handler = CreateReviewHandler(db, userMock.Object);

        var feedbackJson = "{\"rootCause\": \"Insulator Crack\", \"riskOfArcing\": true}";
        var command = new ReviewDetectedAnomalyCommand(
            missionId, anomaly.Id, "Confirmed", "Severe flashover danger", feedbackJson, "Critical");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("Confirmed");
        result.MaintenanceTaskId.Should().NotBeNullOrEmpty();

        var updatedAnomaly = await db.DetectedAnomalies.FindAsync(anomaly.Id);
        updatedAnomaly!.ValidationStatus.Should().Be("Confirmed");
        updatedAnomaly.AnalystFeedback.Should().Be(feedbackJson);
        updatedAnomaly.TowerId.Should().Be(towerId);

        // The alert's tower is derived through the anomaly (EmergencyAlert has no TowerId column).
        var createdAlert = await db.EmergencyAlerts.Include(a => a.Anomaly).FirstOrDefaultAsync(a => a.AnomalyId == anomaly.Id);
        createdAlert.Should().NotBeNull();
        createdAlert!.Anomaly!.TowerId.Should().Be(towerId);

        // The ticket's tower is ticket.Anomaly.TowerId.
        var ticket = await db.MaintenanceTickets.Include(t => t.Anomaly).FirstOrDefaultAsync(t => t.AnomalyId == anomaly.Id);
        ticket.Should().NotBeNull();
        ticket!.Anomaly!.TowerId.Should().Be(towerId);

        // Auto-created ticket is unassigned: NULL technician (FK-safe), stays Open.
        ticket.TechnicianId.Should().BeNull();
        ticket.AssignedAt.Should().BeNull();
        ticket.Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public void MaintenanceTicket_StartProgress_WithoutTechnician_Throws()
    {
        var ticket = new MaintenanceTicket { Id = Guid.NewGuid(), Status = TicketStatus.Open, TechnicianId = null };

        var act = () => ticket.StartProgress();

        act.Should().Throw<InvalidOperationException>().WithMessage("*no technician*");
        ticket.Status.Should().Be(TicketStatus.Open);

        ticket.AssignToTechnician(Guid.NewGuid());
        ticket.StartProgress();
        ticket.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public async Task ReviewDetectedAnomaly_Confirm_FallsBackToMediaTower()
    {
        var userMock = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(userMock.Object);

        var towerId = Guid.NewGuid();
        db.Towers.Add(new Tower { Id = towerId, RegionId = Guid.NewGuid(), TowerCode = "T-101" });
        var media = new InspectionMedia { Id = Guid.NewGuid(), MissionId = Guid.NewGuid(), TowerId = towerId, FileUrl = "/m.jpg", MediaType = "Image" };
        db.InspectionMedia.Add(media);
        var anomaly = new DetectedAnomaly { Id = Guid.NewGuid(), MediaId = media.Id, ConfidenceScore = 0.8, ValidationStatus = "Pending" };
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        var handler = CreateReviewHandler(db, userMock.Object);
        var result = await handler.Handle(
            new ReviewDetectedAnomalyCommand(media.MissionId, anomaly.Id, "Confirmed", "ok"), CancellationToken.None);

        result.Status.Should().Be("Confirmed");
        (await db.DetectedAnomalies.FindAsync(anomaly.Id))!.TowerId.Should().Be(towerId);
    }

    [Fact]
    public async Task ReviewDetectedAnomaly_Confirm_WithoutAnyTower_ThrowsTowerRequired()
    {
        var userMock = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(userMock.Object);

        var media = new InspectionMedia { Id = Guid.NewGuid(), MissionId = Guid.NewGuid(), TowerId = null, FileUrl = "/v.mp4", MediaType = "Video" };
        db.InspectionMedia.Add(media);
        var anomaly = new DetectedAnomaly { Id = Guid.NewGuid(), MediaId = media.Id, ConfidenceScore = 0.8, ValidationStatus = "Pending" };
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        var handler = CreateReviewHandler(db, userMock.Object);
        var act = async () => await handler.Handle(
            new ReviewDetectedAnomalyCommand(media.MissionId, anomaly.Id, "Confirmed", "ok"), CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("tower");
        db.ChangeTracker.Clear();
        (await db.DetectedAnomalies.FindAsync(anomaly.Id))!.ValidationStatus.Should().Be("Pending");
        (await db.MaintenanceTickets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TriggerEmergencyAlert_CreatesAlertWithTowerDerivedFromAnomaly()
    {
        var userMock = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(userMock.Object);

        var towerId = Guid.NewGuid();
        db.Towers.Add(new Tower { Id = towerId, RegionId = Guid.NewGuid(), TowerCode = "T-200" });

        var anomaly = new DetectedAnomaly { Id = Guid.NewGuid(), TowerId = towerId };
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        var alertRepo = new GenericRepository<EmergencyAlert>(db);
        var accessFilter = new GeographicAccessFilter(db, userMock.Object);
        var towerRepo = new TowerRepository(db, accessFilter);
        var anomalyRepo = new AnomalyRepository(db);
        var uow = new UnitOfWork(db);

        var handler = new TriggerEmergencyAlertCommandHandler(alertRepo, towerRepo, anomalyRepo, uow);
        var command = new TriggerEmergencyAlertCommand(Guid.NewGuid(), anomaly.Id, null, "Emergency", "Flashover warning");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.AnomalyId.Should().Be(anomaly.Id);
        result.Priority.Should().Be("Emergency");
        result.TowerId.Should().Be(towerId);
        result.TowerCode.Should().Be("T-200");
        (await db.EmergencyAlerts.CountAsync(a => a.AnomalyId == anomaly.Id)).Should().Be(1);

        // A second Active alert for the same anomaly would break UX_EmergencyAlerts_ActivePerAnomaly.
        var again = () => handler.Handle(command, CancellationToken.None);
        (await again.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("ALERT_ALREADY_ACTIVE");
        (await db.EmergencyAlerts.CountAsync(a => a.AnomalyId == anomaly.Id)).Should().Be(1);
    }

    [Fact]
    public async Task TriggerEmergencyAlert_LinksRequestedTower_WhenAnomalyHasNone()
    {
        var userMock = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(userMock.Object);

        var towerId = Guid.NewGuid();
        db.Towers.Add(new Tower { Id = towerId, RegionId = Guid.NewGuid(), TowerCode = "T-201" });
        var anomaly = new DetectedAnomaly { Id = Guid.NewGuid(), TowerId = null };
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        var accessFilter = new GeographicAccessFilter(db, userMock.Object);
        var handler = new TriggerEmergencyAlertCommandHandler(
            new GenericRepository<EmergencyAlert>(db), new TowerRepository(db, accessFilter), new AnomalyRepository(db), new UnitOfWork(db));

        var result = await handler.Handle(new TriggerEmergencyAlertCommand(Guid.NewGuid(), anomaly.Id, towerId), CancellationToken.None);

        result.TowerId.Should().Be(towerId);
        (await db.DetectedAnomalies.FindAsync(anomaly.Id))!.TowerId.Should().Be(towerId);
    }

    [Fact]
    public async Task CreateMaintenanceTicket_TowerIsDerivedFromAnomaly()
    {
        var techId = Guid.NewGuid();
        var userMock = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(userMock.Object);

        db.Users.Add(new User { Id = techId, FullName = "Technician A", Status = "Active" });

        var towerId = Guid.NewGuid();
        db.Towers.Add(new Tower { Id = towerId, RegionId = Guid.NewGuid(), TowerCode = "T-300" });

        var anomaly = new DetectedAnomaly { Id = Guid.NewGuid(), TowerId = towerId };
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        var ticketRepo = new GenericRepository<MaintenanceTicket>(db);
        var accessFilter = new GeographicAccessFilter(db, userMock.Object);
        var towerRepo = new TowerRepository(db, accessFilter);
        var anomalyRepo = new AnomalyRepository(db);
        var userRepo = new UserRepository(db);
        var uow = new UnitOfWork(db);

        var handler = new CreateMaintenanceTicketCommandHandler(
            ticketRepo, towerRepo, anomalyRepo, userRepo, userMock.Object, uow);

        var command = new CreateMaintenanceTicketCommand(
            anomaly.Id, techId, "High", "Replace cracked insulator");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.TowerId.Should().Be(towerId);
        result.TechnicianId.Should().Be(techId);
        result.Status.Should().Be("Open");
    }

    [Fact]
    public async Task UploadMaintenanceProof_ValidatesProofTypeAndStoresRecord()
    {
        var techId = Guid.NewGuid();
        var userMock = CreateUserMock(techId);
        await using var db = CreateContext(userMock.Object);

        var ticket = new MaintenanceTicket { Id = Guid.NewGuid(), Status = TicketStatus.InProgress };
        db.MaintenanceTickets.Add(ticket);
        await db.SaveChangesAsync();

        var ticketRepo = new GenericRepository<MaintenanceTicket>(db);
        var proofRepo = new GenericRepository<MaintenanceProof>(db);
        var fileStorage = new Mock<IFileStorageService>();
        fileStorage.Setup(f => f.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/images/proof123.jpg");
        var uow = new UnitOfWork(db);

        var handler = new UploadMaintenanceProofCommandHandler(
            ticketRepo, proofRepo, fileStorage.Object, userMock.Object, uow);

        var fileMock = new Mock<IFormFile>();
        var content = "dummy image data";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.FileName).Returns("proof_after.jpg");
        fileMock.Setup(f => f.Length).Returns(ms.Length);

        var command = new UploadMaintenanceProofCommand(ticket.Id, "After", fileMock.Object, "Repaired successfully");
        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.ProofType.Should().Be("After");
        result.FileUrl.Should().Be("/images/proof123.jpg");
        result.TechnicianNotes.Should().Be("Repaired successfully");
    }

    [Fact]
    public async Task CompleteMaintenanceTicket_RequiresProof_ThenCloses()
    {
        var techId = Guid.NewGuid();
        var userMock = CreateUserMock(techId);
        await using var db = CreateContext(userMock.Object);

        var ticket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            AnomalyId = Guid.NewGuid(),
            Status = TicketStatus.InProgress
        };
        db.MaintenanceTickets.Add(ticket);
        await db.SaveChangesAsync();

        var ticketRepo = new GenericRepository<MaintenanceTicket>(db);
        var proofRepo = new GenericRepository<MaintenanceProof>(db);
        var uow = new UnitOfWork(db);

        var handler = new CompleteMaintenanceTicketCommandHandler(ticketRepo, proofRepo, uow);

        // Attempt completion without proof -> should throw
        var command = new CompleteMaintenanceTicketCommand(ticket.Id);
        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*PROOF_REQUIRED*");

        // Add repair proof
        db.MaintenanceProofs.Add(new MaintenanceProof
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ProofType = "After",
            FileUrl = "/images/after.jpg"
        });
        await db.SaveChangesAsync();

        // Complete ticket successfully
        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("Closed");
        result.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MaintenanceTicketService_CreateAndAssign_QueuesMaintenanceTicketAssignedEventInOutbox()
    {
        var managerId = Guid.NewGuid();
        var techId = Guid.NewGuid();
        var userMock = CreateUserMock(managerId);
        await using var db = CreateContext(userMock.Object);

        var role = new Role { Id = 10, RoleName = UserRoles.MaintenanceTechnician };
        var tech = new User { Id = techId, FullName = "Kỹ thuật viên A", Email = "tech@evn.vn", Status = "Active" };
        db.Roles.Add(role);
        db.Users.Add(tech);
        db.UserRoles.Add(new UserRole { UserId = techId, RoleId = role.Id, Role = role, User = tech });

        var towerId = Guid.NewGuid();
        db.Towers.Add(new Tower { Id = towerId, RegionId = Guid.NewGuid(), TowerCode = "T-500" });

        var anomaly1 = new DetectedAnomaly { Id = Guid.NewGuid(), TowerId = towerId };
        var anomaly2 = new DetectedAnomaly { Id = Guid.NewGuid(), TowerId = towerId };
        var unassignedTicket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            TicketCode = "TCK-20261112-099",
            AnomalyId = anomaly2.Id,
            Anomaly = anomaly2,
            ManagerId = managerId,
            TechnicianId = null,
            Status = TicketStatus.Open,
            Priority = TicketPriority.High,
            Description = "Phiếu tự sinh chờ giao kỹ thuật viên"
        };
        db.DetectedAnomalies.AddRange(anomaly1, anomaly2);
        db.MaintenanceTickets.Add(unassignedTicket);
        await db.SaveChangesAsync();

        var service = new MaintenanceTicketService(db, userMock.Object);

        // 1. CreateTicketAsync should queue MaintenanceTicketAssignedEvent in OutboxMessages
        var createdDto = await service.CreateTicketAsync(new CreateMaintenanceTicketRequest
        {
            AnomalyId = anomaly1.Id,
            TechnicianId = techId,
            Priority = "High",
            Description = "Thay thế chuỗi cách điện pha A",
            DueDate = DateTime.UtcNow.AddDays(3)
        });

        createdDto.TowerId.Should().Be(towerId);

        // 2. AssignTechnicianAsync should also queue MaintenanceTicketAssignedEvent in OutboxMessages
        var assignedDto = await service.AssignTechnicianAsync(unassignedTicket.Id, new AssignTechnicianRequest
        {
            TechnicianId = techId,
            DueDate = DateTime.UtcNow.AddDays(2)
        });

        assignedDto.TechnicianId.Should().Be(techId);

        var outboxMessages = await db.OutboxMessages
            .Where(m => m.MessageType == nameof(MaintenanceTicketAssignedEvent))
            .ToListAsync();

        outboxMessages.Should().HaveCount(2);
        outboxMessages.Should().Contain(m => m.Payload.Contains(createdDto.Id.ToString()));
        outboxMessages.Should().Contain(m => m.Payload.Contains(unassignedTicket.Id.ToString()));
    }

    [Fact]
    public async Task MonitorRepository_MissionStatusOverviewAndSummary_CountAllWorkflowStatuses()
    {
        var userMock = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(userMock.Object);

        var managerId = Guid.NewGuid();
        db.Missions.AddRange(
            new Mission { Id = Guid.NewGuid(), MissionCode = "M-1", Title = "Draft", ManagerId = managerId, Status = MissionStatus.Draft },
            new Mission { Id = Guid.NewGuid(), MissionCode = "M-2", Title = "PendingAcceptance", ManagerId = managerId, Status = MissionStatus.PendingAcceptance },
            new Mission { Id = Guid.NewGuid(), MissionCode = "M-3", Title = "Assigned", ManagerId = managerId, Status = MissionStatus.Assigned },
            new Mission { Id = Guid.NewGuid(), MissionCode = "M-4", Title = "Preparing", ManagerId = managerId, Status = MissionStatus.Preparing },
            new Mission { Id = Guid.NewGuid(), MissionCode = "M-5", Title = "Ready", ManagerId = managerId, Status = MissionStatus.Ready },
            new Mission { Id = Guid.NewGuid(), MissionCode = "M-6", Title = "Postponed", ManagerId = managerId, Status = MissionStatus.Postponed },
            new Mission { Id = Guid.NewGuid(), MissionCode = "M-7", Title = "InProgress", ManagerId = managerId, Status = MissionStatus.InProgress },
            new Mission { Id = Guid.NewGuid(), MissionCode = "M-8", Title = "Suspended", ManagerId = managerId, Status = MissionStatus.Suspended },
            new Mission { Id = Guid.NewGuid(), MissionCode = "M-9", Title = "Completed", ManagerId = managerId, Status = MissionStatus.Completed }
        );
        await db.SaveChangesAsync();

        var repo = new MonitorRepository(db);
        var overview = await repo.GetMissionStatusOverviewAsync(CancellationToken.None);
        var summary = await repo.GetSummaryAsync(CancellationToken.None);

        overview.Pending.Should().Be(6);
        overview.InProgress.Should().Be(2);
        overview.Completed.Should().Be(1);

        summary.TotalMissions.Should().Be(9);
        summary.PendingMissions.Should().Be(6);
        summary.InProgressMissions.Should().Be(2);
        summary.CompletedMissions.Should().Be(1);
    }
}
