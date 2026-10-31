using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// Tickets created when an analyst confirms an anomaly have no technician, so MF04 needs
/// PUT /maintenance/tickets/{id}/assign and GET /maintenance/tickets/technicians before repair can start.
/// </summary>
[Collection(PostgresCollection.Name)]
public class MaintenanceAssignmentTests
{
    private readonly PostgresFixture _pg;

    public MaintenanceAssignmentTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task AssignTechnician_OpenTicketWithoutTechnician_AssignsListsAndNotifies()
    {
        if (!_pg.Available) return;

        var tag = Guid.NewGuid().ToString("N")[..8];
        Guid managerId, technicianId, inspectorId, ticketId;
        await using (var db = _pg.CreateContext())
        {
            managerId = await InsertUserAsync(db, $"m-{tag}@it.test", "Manager");
            technicianId = await InsertUserAsync(db, $"t-{tag}@it.test", "MaintenanceTechnician");
            inspectorId = await InsertUserAsync(db, $"i-{tag}@it.test", "Inspector");
            ticketId = await SeedUnassignedTicketAsync(db, tag, managerId);
        }

        var current = new Mock<ICurrentUserServices>();
        current.SetupGet(u => u.UserId).Returns(managerId);
        current.SetupGet(u => u.IsAuthenticated).Returns(true);
        current.SetupGet(u => u.Roles).Returns(new[] { UserRoles.Manager });

        await using (var db = _pg.CreateContext(current.Object))
        {
            var service = new MaintenanceTicketService(db, current.Object);

            var technicians = await service.GetTechniciansAsync();
            technicians.Should().Contain(t => t.Id == technicianId && t.ActiveTicketCount == 0);
            technicians.Should().NotContain(t => t.Id == inspectorId);

            var notTechnician = () => service.AssignTechnicianAsync(ticketId, new AssignTechnicianRequest { TechnicianId = inspectorId });
            await notTechnician.Should().ThrowAsync<BusinessRuleException>();

            var dto = await service.AssignTechnicianAsync(ticketId, new AssignTechnicianRequest { TechnicianId = technicianId, DueDate = DateTime.UtcNow.AddDays(3) });
            dto.TechnicianId.Should().Be(technicianId);
            dto.AssignedAt.Should().NotBeNull();

            (await service.GetTechniciansAsync()).Single(t => t.Id == technicianId).ActiveTicketCount.Should().Be(1);

            // The ticket can now move on to InProgress, which needs an assigned technician.
            (await service.UpdateTicketStatusAsync(ticketId, "InProgress")).Status.Should().Be("InProgress");

            var reassign = () => service.AssignTechnicianAsync(ticketId, new AssignTechnicianRequest { TechnicianId = technicianId });
            await reassign.Should().ThrowAsync<BusinessRuleException>().WithMessage("TICKET_NOT_OPEN*");
        }

        await using (var db = _pg.CreateContext())
        {
            (await db.Notifications.CountAsync(n => n.UserId == technicianId && n.Type == "MAINTENANCE_ASSIGNED" && n.ReferenceId == ticketId))
                .Should().Be(1);
        }
    }

    [Fact]
    public async Task Close_RequiresManagerAndPendingVerification_AndLocksTheTicket()
    {
        if (!_pg.Available) return;

        var tag = Guid.NewGuid().ToString("N")[..8];
        Guid managerId, technicianId, ticketId;
        await using (var db = _pg.CreateContext())
        {
            managerId = await InsertUserAsync(db, $"m-{tag}@it.test", "Manager");
            technicianId = await InsertUserAsync(db, $"t-{tag}@it.test", "MaintenanceTechnician");
            ticketId = await SeedUnassignedTicketAsync(db, tag, managerId);
            var ticket = await db.MaintenanceTickets.SingleAsync(t => t.Id == ticketId);
            ticket.TechnicianId = technicianId;
            ticket.Status = TicketStatus.InProgress;
            db.MaintenanceProofs.Add(new MaintenanceProof { TicketId = ticketId, UploadedBy = technicianId, FileUrl = "uav-media/proof.jpg", TechnicianNotes = "" });
            await db.SaveChangesAsync();
        }

        await using (var db = _pg.CreateContext(Current(managerId, UserRoles.Manager)))
        {
            var close = () => new MaintenanceTicketService(db, Current(managerId, UserRoles.Manager)).CloseTicketAsync(ticketId);
            await close.Should().ThrowAsync<BusinessRuleException>().WithMessage("TICKET_NOT_PENDING_VERIFICATION*");
        }

        await using (var db = _pg.CreateContext(Current(technicianId, UserRoles.MaintenanceTechnician)))
        {
            var technician = new MaintenanceTicketService(db, Current(technicianId, UserRoles.MaintenanceTechnician));
            (await technician.UpdateTicketStatusAsync(ticketId, "PendingVerification")).Status.Should().Be("PendingVerification");

            var selfClose = () => technician.UpdateTicketStatusAsync(ticketId, "Closed");
            await selfClose.Should().ThrowAsync<ForbiddenException>().WithMessage("TICKET_CLOSE_REQUIRES_MANAGER*");
        }

        await using (var db = _pg.CreateContext(Current(managerId, UserRoles.Manager)))
        {
            var manager = new MaintenanceTicketService(db, Current(managerId, UserRoles.Manager));
            (await manager.UpdateTicketStatusAsync(ticketId, "Closed")).Status.Should().Be("Closed");

            var lateMaterial = () => manager.LogMaterialAsync(ticketId, new LogMaterialRequest { ComponentName = "Insulator", Unit = "pcs", QuantityUsed = 1 });
            await lateMaterial.Should().ThrowAsync<BusinessRuleException>().WithMessage("TICKET_CLOSED*");

            var list = await manager.GetTicketsAsync(0, 50, "Closed", null);
            var row = list.Items.Single(t => t.Id == ticketId);
            row.TowerId.Should().NotBeNull();
            row.Technician.Should().NotBeNull();
        }

        await using (var db = _pg.CreateContext())
        {
            var anomalyId = (await db.MaintenanceTickets.SingleAsync(t => t.Id == ticketId)).AnomalyId;
            (await db.DetectedAnomalies.SingleAsync(a => a.Id == anomalyId)).ValidationStatus.Should().Be("Resolved");
        }
    }

    private static ICurrentUserServices Current(Guid userId, string role)
    {
        var current = new Mock<ICurrentUserServices>();
        current.SetupGet(u => u.UserId).Returns(userId);
        current.SetupGet(u => u.IsAuthenticated).Returns(true);
        current.SetupGet(u => u.Roles).Returns(new[] { role });
        return current.Object;
    }

    private static async Task<Guid> SeedUnassignedTicketAsync(ApplicationDbContext db, string tag, Guid managerId)
    {
        var region = new Region { Id = Guid.NewGuid(), Code = $"R-{tag}", Name = "Region", Type = "Province" };
        var tower = new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = $"T-{tag}", Geom = new Point(105.85, 21.03) { SRID = 4326 } };
        var mission = new Mission { Id = Guid.NewGuid(), MissionCode = $"M-{tag}", Title = "Mission", ManagerId = managerId, Status = MissionStatus.InProgress, RegionId = region.Id };
        var category = new DefectCategory { CategoryCode = $"C-{tag}", CategoryName = "Insulator crack", Description = "", SeverityWeight = 1 };
        db.AddRange(region, tower, mission, category);
        await db.SaveChangesAsync();

        var media = new InspectionMedia { Id = Guid.NewGuid(), MissionId = mission.Id, TowerId = tower.Id, MediaType = "Image", ValidationStatus = "PendingReview", FileUrl = "uav-media/it.jpg", CapturedAt = DateTime.UtcNow };
        var run = new AIAnalysisRun { Id = Guid.NewGuid(), MediaId = media.Id, ExecutionLocation = AIAnalysisRun.Cloud, ModelName = "MockAI", Status = AIAnalysisStatus.Completed };
        var anomaly = new DetectedAnomaly { Id = Guid.NewGuid(), MediaId = media.Id, AnalysisRunId = run.Id, CategoryId = category.Id, TowerId = tower.Id, BoundingBox = "{}", ConfidenceScore = 0.9, ValidationStatus = "Confirmed", AnalystNotes = "" };
        var ticket = new MaintenanceTicket { Id = Guid.NewGuid(), TicketCode = $"TK-{tag}", AnomalyId = anomaly.Id, ManagerId = managerId, Status = TicketStatus.Open, Description = "Fix" };
        db.AddRange(media, run, anomaly, ticket);
        await db.SaveChangesAsync();
        return ticket.Id;
    }

    /// <summary>Users carry PasswordHash, which only IdentityService maps, so they are inserted in SQL.</summary>
    private static async Task<Guid> InsertUserAsync(ApplicationDbContext db, string email, string role)
    {
        var userId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users" ("Id", "FullName", "Email", "Phone", "Status", "IsEmailVerified", "PasswordHash", "CreatedAt", "IsDeleted")
            VALUES ({userId}, 'IT user', {email}, '', 'Active', true, 'x', now(), false)
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Roles" ("RoleName", "Description") VALUES ({role}, '') ON CONFLICT ("RoleName") DO NOTHING
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserRoles" ("UserId", "RoleId", "AssignedAt")
            SELECT {userId}, "Id", now() FROM "Roles" WHERE "RoleName" = {role}
            """);
        return userId;
    }
}
