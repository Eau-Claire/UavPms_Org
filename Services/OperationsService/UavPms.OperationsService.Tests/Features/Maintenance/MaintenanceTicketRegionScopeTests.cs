using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Maintenance;

/// <summary>
/// Spec §4: phiếu bảo trì theo vùng. SystemAdmin thấy mọi phiếu; Manager thấy phiếu mình phụ trách hoặc trong vùng
/// (vùng theo cột điện của defect, defect chưa gắn cột thì theo vùng nhiệm vụ); Technician thấy phiếu được giao.
/// </summary>
public class MaintenanceTicketRegionScopeTests
{
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _technicianId = Guid.NewGuid();
    private readonly Guid _ownRegion = Guid.NewGuid();
    private readonly Guid _otherRegion = Guid.NewGuid();

    private static Mock<ICurrentUserServices> User(Guid id, string role)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(id);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        return mock;
    }

    /// <summary>
    /// 4 phiếu: InRegionByTower (cột thuộc vùng Manager), InRegionByMission (defect chưa gắn cột, nhiệm vụ thuộc vùng Manager),
    /// OtherRegion (vùng khác, Manager khác phụ trách) và AssignedToTechnician (vùng khác, giao cho Technician).
    /// </summary>
    private async Task<(ApplicationDbContext Db, Guid InTower, Guid InMission, Guid Other, Guid Assigned)> SeedAsync(ICurrentUserServices user)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, user);

        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = _managerId, RegionId = _ownRegion });
        var ownTower = new Tower { Id = Guid.NewGuid(), TowerCode = "T-OWN", RegionId = _ownRegion };
        var otherTower = new Tower { Id = Guid.NewGuid(), TowerCode = "T-OTHER", RegionId = _otherRegion };
        var ownMission = new Mission { Id = Guid.NewGuid(), RegionId = _ownRegion };
        var media = new InspectionMedia { Id = Guid.NewGuid(), MissionId = ownMission.Id, Mission = ownMission };
        db.AddRange(ownTower, otherTower, ownMission, media);

        // Chi tiết phiếu Include các quan hệ bắt buộc (Category, Manager, Technician) nên seed đủ bản ghi liên kết.
        var category = new DefectCategory { Id = 1, CategoryCode = "BIRD_NEST", CategoryName = "Bird nest" };
        var otherManager = new User { Id = Guid.NewGuid(), Email = "other-manager@x.vn" };
        db.AddRange(category, otherManager,
            new User { Id = _managerId, Email = "manager@x.vn" },
            new User { Id = _technicianId, Email = "technician@x.vn" });

        DetectedAnomaly Anomaly(Tower? tower) => new()
            { Id = Guid.NewGuid(), MediaId = media.Id, Media = media, TowerId = tower?.Id, Tower = tower, CategoryId = category.Id };
        MaintenanceTicket Ticket(DetectedAnomaly anomaly, Guid? technicianId = null) => new()
        {
            Id = Guid.NewGuid(), TicketCode = Guid.NewGuid().ToString("N")[..8], AnomalyId = anomaly.Id, Anomaly = anomaly,
            ManagerId = otherManager.Id, TechnicianId = technicianId, Status = TicketStatus.PendingVerification
        };

        var inTower = Ticket(Anomaly(ownTower));
        var inMission = Ticket(Anomaly(null));
        var other = Ticket(Anomaly(otherTower));
        var assigned = Ticket(Anomaly(otherTower), _technicianId);
        db.MaintenanceTickets.AddRange(inTower, inMission, other, assigned);
        await db.SaveChangesAsync();
        return (db, inTower.Id, inMission.Id, other.Id, assigned.Id);
    }

    [Fact]
    public async Task GetTickets_FiltersByRoleAndRegion()
    {
        var manager = User(_managerId, UserRoles.Manager).Object;
        var (db, inTower, inMission, other, assigned) = await SeedAsync(manager);
        await using var _ = db;

        var managerTickets = await new MaintenanceTicketService(db, manager).GetTicketsAsync(0, 50, null, null);
        var technicianTickets = await new MaintenanceTicketService(db, User(_technicianId, UserRoles.Technician).Object)
            .GetTicketsAsync(0, 50, null, null);
        var adminTickets = await new MaintenanceTicketService(db, User(Guid.NewGuid(), UserRoles.SystemAdmin).Object)
            .GetTicketsAsync(0, 50, null, null);

        managerTickets.Items.Select(t => t.Id).Should().BeEquivalentTo(new[] { inTower, inMission });
        technicianTickets.Items.Select(t => t.Id).Should().BeEquivalentTo(new[] { assigned });
        adminTickets.TotalCount.Should().Be(4);
        managerTickets.Items.Should().NotContain(t => t.Id == other);
    }

    [Fact]
    public async Task Manager_CannotReadOrCloseTicketOutsideRegion_ButCanInsideRegion()
    {
        var manager = User(_managerId, UserRoles.Manager).Object;
        var (db, inTower, _, other, _) = await SeedAsync(manager);
        await using var _db = db;
        var service = new MaintenanceTicketService(db, manager);

        var readOther = () => service.GetTicketByIdAsync(other);
        var closeOther = () => service.CloseTicketAsync(other);

        await readOther.Should().ThrowAsync<ForbiddenException>();
        await closeOther.Should().ThrowAsync<ForbiddenException>().WithMessage("TICKET_REGION_SCOPE_REQUIRED");
        (await service.GetTicketByIdAsync(inTower)).Id.Should().Be(inTower);
    }

    [Fact]
    public async Task Technician_CannotReadTicketNotAssignedToThem()
    {
        var technician = User(_technicianId, UserRoles.Technician).Object;
        var (db, inTower, _, _, assigned) = await SeedAsync(technician);
        await using var _db = db;
        var service = new MaintenanceTicketService(db, technician);

        var readOther = () => service.GetTicketByIdAsync(inTower);

        await readOther.Should().ThrowAsync<ForbiddenException>();
        (await service.GetTicketByIdAsync(assigned)).Id.Should().Be(assigned);
    }
}
