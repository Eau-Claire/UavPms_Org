using System;
using System.Collections.Generic;
using System.IO;
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
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Authorization;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
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

    [Fact]
    public async Task ReviewDetectedAnomaly_StoresAnalystFeedbackAndEscalatesEmergency()
    {
        var analystId = Guid.NewGuid();
        var userMock = CreateUserMock(analystId);
        await using var db = CreateContext(userMock.Object);

        var towerId = Guid.NewGuid();
        var tower = new Tower { Id = towerId, TowerCode = "T-100", ActiveDefectsCount = 0 };
        db.Towers.Add(tower);

        var missionId = Guid.NewGuid();
        var anomaly = new DetectedAnomaly
        {
            Id = Guid.NewGuid(),
            TowerId = towerId.ToString(),
            ConfidenceScore = 0.95,
            ValidationStatus = "Pending"
        };
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        var anomalyRepo = new AnomalyRepository(db);
        var missionRepo = new MissionRepository(db);
        var accessFilter = new GeographicAccessFilter(db, userMock.Object);
        var towerRepo = new TowerRepository(db, accessFilter);
        var alertRepo = new GenericRepository<EmergencyAlert>(db);
        var ticketRepo = new GenericRepository<MaintenanceTicket>(db);
        var uow = new UnitOfWork(db);

        var handler = new ReviewDetectedAnomalyCommandHandler(
            anomalyRepo, missionRepo, towerRepo, alertRepo, ticketRepo, userMock.Object, uow);

        var feedbackJson = "{\"rootCause\": \"Insulator Crack\", \"riskOfArcing\": true}";
        var command = new ReviewDetectedAnomalyCommand(
            missionId, anomaly.Id, "Confirmed", "Severe flashover danger", feedbackJson, "Critical");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("Confirmed");

        var updatedAnomaly = await db.DetectedAnomalies.FindAsync(anomaly.Id);
        updatedAnomaly!.ValidationStatus.Should().Be("Confirmed");
        updatedAnomaly.AnalystFeedback.Should().Be(feedbackJson);

        var updatedTower = await db.Towers.FindAsync(towerId);
        updatedTower!.ActiveDefectsCount.Should().Be(1);
        updatedTower.RiskLevel.Should().Be("Critical");

        var createdAlert = await db.EmergencyAlerts.FirstOrDefaultAsync(a => a.AnomalyId == anomaly.Id);
        createdAlert.Should().NotBeNull();
        createdAlert!.TowerId.Should().Be(towerId);
    }

    [Fact]
    public async Task TriggerEmergencyAlert_CreatesAlertAndIncrementsDefects()
    {
        var userMock = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(userMock.Object);

        var towerId = Guid.NewGuid();
        var tower = new Tower { Id = towerId, TowerCode = "T-200", ActiveDefectsCount = 1 };
        db.Towers.Add(tower);

        var anomaly = new DetectedAnomaly { Id = Guid.NewGuid(), TowerId = towerId.ToString() };
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        var alertRepo = new GenericRepository<EmergencyAlert>(db);
        var accessFilter = new GeographicAccessFilter(db, userMock.Object);
        var towerRepo = new TowerRepository(db, accessFilter);
        var anomalyRepo = new AnomalyRepository(db);
        var uow = new UnitOfWork(db);

        var handler = new TriggerEmergencyAlertCommandHandler(alertRepo, towerRepo, anomalyRepo, uow);
        var command = new TriggerEmergencyAlertCommand(Guid.NewGuid(), anomaly.Id, towerId, null, "Emergency", "Flashover warning");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.AnomalyId.Should().Be(anomaly.Id);
        result.Priority.Should().Be("Emergency");

        var updatedTower = await db.Towers.FindAsync(towerId);
        updatedTower!.ActiveDefectsCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateMaintenanceTicket_LinksToTowerDirectly()
    {
        var techId = Guid.NewGuid();
        var userMock = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(userMock.Object);

        db.Users.Add(new User { Id = techId, FullName = "Technician A", Status = "Active" });

        var towerId = Guid.NewGuid();
        var tower = new Tower { Id = towerId, TowerCode = "T-300", ActiveDefectsCount = 0 };
        db.Towers.Add(tower);

        var anomaly = new DetectedAnomaly { Id = Guid.NewGuid(), TowerId = towerId.ToString() };
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
            anomaly.Id, techId, "High", "Replace cracked insulator", towerId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.TowerId.Should().Be(towerId);
        result.TechnicianId.Should().Be(techId);
        result.Status.Should().Be("Open");

        var updatedTower = await db.Towers.FindAsync(towerId);
        updatedTower!.ActiveDefectsCount.Should().Be(1);
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
    public async Task CompleteMaintenanceTicket_RequiresProof_DecrementsTowerDefects_AndLogsHistory()
    {
        var techId = Guid.NewGuid();
        var userMock = CreateUserMock(techId);
        await using var db = CreateContext(userMock.Object);

        var towerId = Guid.NewGuid();
        var tower = new Tower { Id = towerId, TowerCode = "T-500", ActiveDefectsCount = 1, RiskLevel = "Medium" };
        db.Towers.Add(tower);

        var ticket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            TowerId = towerId,
            Status = TicketStatus.InProgress
        };
        db.MaintenanceTickets.Add(ticket);
        await db.SaveChangesAsync();

        var ticketRepo = new GenericRepository<MaintenanceTicket>(db);
        var proofRepo = new GenericRepository<MaintenanceProof>(db);
        var accessFilter = new GeographicAccessFilter(db, userMock.Object);
        var towerRepo = new TowerRepository(db, accessFilter);
        var conditionRepo = new GenericRepository<TowerConditionHistory>(db);
        var uow = new UnitOfWork(db);

        var handler = new CompleteMaintenanceTicketCommandHandler(
            ticketRepo, proofRepo, towerRepo, conditionRepo, uow);

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

        var updatedTower = await db.Towers.FindAsync(towerId);
        updatedTower!.ActiveDefectsCount.Should().Be(0);
        updatedTower.RiskLevel.Should().Be("Normal");

        var histories = await db.TowerConditionHistories.Where(h => h.TowerId == towerId).ToListAsync();
        histories.Should().HaveCount(1);
        histories[0].ActiveDefectsCount.Should().Be(0);
        histories[0].RiskLevel.Should().Be("Normal");
    }
}
