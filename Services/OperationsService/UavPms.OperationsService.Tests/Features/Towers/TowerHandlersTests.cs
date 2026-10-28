using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Towers.Commands;
using UavPms.OperationsService.Application.Features.Towers.Queries;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Authorization;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Tests.Features.Towers;

public class TowerHandlersTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId, string role = UserRoles.SystemAdmin)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
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

    private static CreateTowerCommandHandler CreateHandler(ApplicationDbContext db, ICurrentUserServices user) =>
        new(new RegionRepository(db), new TowerRepository(db, new GeographicAccessFilter(db, user)), new UnitOfWork(db));

    [Fact]
    public async Task CreateTower_ValidRequest_PersistsTowerInRegionWithPointGeometry()
    {
        var user = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(user.Object);
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01", Name = "Region 1" };
        db.Regions.Add(region);
        await db.SaveChangesAsync();

        var result = await CreateHandler(db, user.Object).Handle(
            new CreateTowerCommand(region.Id, "  T-001 ", "Néo", "Xã A", 10.82, 106.63), CancellationToken.None);

        result.TowerCode.Should().Be("T-001");
        result.RegionId.Should().Be(region.Id);
        result.Latitude.Should().Be(10.82);
        result.Longitude.Should().Be(106.63);

        var saved = await db.Towers.SingleAsync();
        saved.RegionId.Should().Be(region.Id);
        saved.TowerCode.Should().Be("T-001");
        saved.Geom.Should().NotBeNull();
        saved.Geom!.SRID.Should().Be(4326);
        saved.Geom.Coordinate.X.Should().Be(106.63);
        saved.Geom.Coordinate.Y.Should().Be(10.82);
    }

    [Fact]
    public async Task CreateTower_UnknownRegion_ThrowsNotFound()
    {
        var user = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(user.Object);

        var act = () => CreateHandler(db, user.Object).Handle(
            new CreateTowerCommand(Guid.NewGuid(), "T-404", null, null, 10, 106), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        (await db.Towers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateTower_DuplicateCode_ThrowsBusinessRule()
    {
        var user = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(user.Object);
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);
        db.Towers.Add(new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = "T-DUP" });
        await db.SaveChangesAsync();

        var act = () => CreateHandler(db, user.Object).Handle(
            new CreateTowerCommand(region.Id, "T-DUP", null, null, 10, 106), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*T-DUP*");
        (await db.Towers.CountAsync()).Should().Be(1);
    }

    [Fact]
    public void CreateTowerValidator_RequiresRegion()
    {
        var validator = new CreateTowerCommandValidator();

        validator.Validate(new CreateTowerCommand(Guid.Empty, "T-1", null, null, 10, 106)).IsValid.Should().BeFalse();
        validator.Validate(new CreateTowerCommand(Guid.NewGuid(), "T-1", null, null, 10, 106)).IsValid.Should().BeTrue();
        validator.Validate(new CreateTowerCommand(Guid.NewGuid(), "", null, null, 10, 106)).IsValid.Should().BeFalse();
        validator.Validate(new CreateTowerCommand(Guid.NewGuid(), "T-1", null, null, 95, 106)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetTowerHistory_PagesFiltersAndCountsStatsOverWholeTower()
    {
        var user = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(user.Object);
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01", Name = "Region 1" };
        var tower = new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = "T-300" };
        db.Regions.Add(region);
        db.Towers.Add(tower);
        var category = new DefectCategory { Id = 1, CategoryCode = "DEF", CategoryName = "Defect" };
        db.DefectCategories.Add(category);
        var mission = new Mission { Id = Guid.NewGuid(), Status = MissionStatus.Completed };
        db.Missions.Add(mission);
        var lastCapture = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
        var media = new InspectionMedia { Id = Guid.NewGuid(), MissionId = mission.Id, TowerId = tower.Id, MediaType = "Image", FileUrl = "/a.jpg", CapturedAt = lastCapture.AddDays(-5) };
        var latest = new InspectionMedia { Id = Guid.NewGuid(), MissionId = mission.Id, TowerId = tower.Id, MediaType = "Image", FileUrl = "/b.jpg", CapturedAt = lastCapture };
        db.InspectionMedia.AddRange(media, latest);

        DetectedAnomaly Anomaly(string status, int day) => new()
        {
            Id = Guid.NewGuid(), MediaId = media.Id, TowerId = tower.Id, CategoryId = category.Id, ValidationStatus = status,
            CreatedAt = new DateTime(2026, 10, day, 0, 0, 0, DateTimeKind.Utc)
        };
        var pending1 = Anomaly("Pending", 1);
        var pending2 = Anomaly("Pending", 2);
        var pending3 = Anomaly("Pending", 3);
        var confirmed = Anomaly("Confirmed", 4);
        var rejected = Anomaly("Rejected", 5);
        db.DetectedAnomalies.AddRange(pending1, pending2, pending3, confirmed, rejected);
        db.MaintenanceTickets.AddRange(
            new MaintenanceTicket { Id = Guid.NewGuid(), TicketCode = "T1", AnomalyId = confirmed.Id, Status = TicketStatus.Open },
            new MaintenanceTicket { Id = Guid.NewGuid(), TicketCode = "T2", AnomalyId = confirmed.Id, Status = TicketStatus.PendingVerification },
            new MaintenanceTicket { Id = Guid.NewGuid(), TicketCode = "T3", AnomalyId = confirmed.Id, Status = TicketStatus.Closed });
        await db.SaveChangesAsync();
        // CreatedAt is overwritten on insert by the audit hook; restore the test timeline.
        foreach (var (a, day) in new[] { (pending1, 1), (pending2, 2), (pending3, 3), (confirmed, 4), (rejected, 5) })
            db.Entry(a).Property(x => x.CreatedAt).CurrentValue = new DateTime(2026, 10, day, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var handler = new GetTowerHistoryQueryHandler(
            new TowerRepository(db, new GeographicAccessFilter(db, user.Object)),
            new AnomalyRepository(db),
            new MaintenanceTicketRepository(db),
            new InspectionMediaRepository(db));

        var page2 = await handler.Handle(
            new GetTowerHistoryQuery(tower.Id, Page: 2, PageSize: 2, ValidationStatus: "pending"), CancellationToken.None);

        page2.Anomalies.Select(a => a.Id).Should().Equal(pending1.Id); // newest first: pending3, pending2 | pending1
        page2.AnomaliesPagination.Should().Be(new UavPms.OperationsService.Application.Common.DTOs.PaginationMetaData(2, 2, 3, 2));
        page2.MaintenancePagination.TotalItems.Should().Be(3);
        page2.Stats.Should().Be(new UavPms.OperationsService.Application.Features.Towers.DTOs.TowerHistoryStatsDto(
            TotalAnomalies: 5, PendingAnomalies: 3, ConfirmedAnomalies: 1, OpenAnomalies: 4,
            TotalTickets: 3, OpenTickets: 1, InProgressTickets: 1, ActiveTickets: 2, LastInspectedAt: lastCapture));

        var window = await handler.Handle(new GetTowerHistoryQuery(tower.Id,
            From: new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), To: new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)), CancellationToken.None);
        window.Anomalies.Select(a => a.Id).Should().Equal(confirmed.Id, pending3.Id, pending2.Id);
    }

    [Fact]
    public void GetTowerHistoryValidator_RejectsUnknownStatusAndInvertedRange()
    {
        var validator = new GetTowerHistoryQueryValidator();
        var id = Guid.NewGuid();

        validator.Validate(new GetTowerHistoryQuery(id, ValidationStatus: "confirmed")).IsValid.Should().BeTrue();
        validator.Validate(new GetTowerHistoryQuery(id, ValidationStatus: "Approved")).IsValid.Should().BeFalse();
        validator.Validate(new GetTowerHistoryQuery(id, PageSize: 101)).IsValid.Should().BeFalse();
        validator.Validate(new GetTowerHistoryQuery(id, From: DateTime.UtcNow, To: DateTime.UtcNow.AddDays(-1))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetTowerHistory_ReturnsCurrentAnomaliesExcludingSuperseded_AndTicketsOfTower()
    {
        var user = CreateUserMock(Guid.NewGuid());
        await using var db = CreateContext(user.Object);
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01", Name = "Region 1" };
        db.Regions.Add(region);
        var tower = new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = "T-100" };
        var otherTower = new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = "T-200" };
        db.Towers.AddRange(tower, otherTower);

        var category = new DefectCategory { Id = 1, CategoryCode = "DEF-INS", CategoryName = "Insulator crack" };
        db.DefectCategories.Add(category);
        var mission = new Mission { Id = Guid.NewGuid(), Status = MissionStatus.Completed };
        db.Missions.Add(mission);
        var media = new InspectionMedia { Id = Guid.NewGuid(), MissionId = mission.Id, TowerId = tower.Id, MediaType = "Image", FileUrl = "/m.jpg" };
        db.InspectionMedia.Add(media);
        var cloudRun = new AIAnalysisRun { Id = Guid.NewGuid(), MediaId = media.Id, ExecutionLocation = AIAnalysisRun.Cloud, ModelName = "YOLOv8-cloud", Status = AIAnalysisStatus.Completed };
        db.AIAnalysisRuns.Add(cloudRun);

        // Edge detection later superseded by the cloud result -> must be hidden.
        var cloudAnomaly = new DetectedAnomaly
        {
            Id = Guid.NewGuid(), MediaId = media.Id, AnalysisRunId = cloudRun.Id, TowerId = tower.Id,
            CategoryId = category.Id, ConfidenceScore = 0.91, ValidationStatus = "Confirmed"
        };
        var supersededEdge = new DetectedAnomaly
        {
            Id = Guid.NewGuid(), MediaId = media.Id, TowerId = tower.Id, SupersededById = cloudAnomaly.Id,
            CategoryId = category.Id, ConfidenceScore = 0.7, ValidationStatus = "Pending"
        };
        var manualAnomaly = new DetectedAnomaly
        {
            Id = Guid.NewGuid(), MediaId = media.Id, TowerId = tower.Id,
            CategoryId = category.Id, ConfidenceScore = 1.0, ValidationStatus = "Pending"
        };
        var otherTowerAnomaly = new DetectedAnomaly
        {
            Id = Guid.NewGuid(), MediaId = media.Id, TowerId = otherTower.Id,
            CategoryId = category.Id, ConfidenceScore = 0.8, ValidationStatus = "Confirmed"
        };
        db.DetectedAnomalies.AddRange(cloudAnomaly, supersededEdge, manualAnomaly, otherTowerAnomaly);

        var ticket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(), TicketCode = "TKT-1", AnomalyId = cloudAnomaly.Id, Status = TicketStatus.Open,
            Priority = TicketPriority.High, Description = "Replace insulator"
        };
        ticket.MaintenanceProofs.Add(new MaintenanceProof { Id = Guid.NewGuid(), TicketId = ticket.Id, ProofType = "Before", FileUrl = "/p.jpg" });
        var otherTicket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(), TicketCode = "TKT-2", AnomalyId = otherTowerAnomaly.Id, Status = TicketStatus.Open, Description = "Other"
        };
        db.MaintenanceTickets.AddRange(ticket, otherTicket);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var handler = new GetTowerHistoryQueryHandler(
            new TowerRepository(db, new GeographicAccessFilter(db, user.Object)),
            new AnomalyRepository(db),
            new MaintenanceTicketRepository(db),
            new InspectionMediaRepository(db));

        var history = await handler.Handle(new GetTowerHistoryQuery(tower.Id), CancellationToken.None);

        history.Tower.TowerCode.Should().Be("T-100");
        history.Anomalies.Select(a => a.Id).Should().BeEquivalentTo(new[] { cloudAnomaly.Id, manualAnomaly.Id });
        history.Anomalies.Single(a => a.Id == cloudAnomaly.Id).AiSource.Should().Be("YOLOv8-cloud");
        history.Anomalies.Single(a => a.Id == cloudAnomaly.Id).MissionId.Should().Be(mission.Id);
        history.Anomalies.Single(a => a.Id == manualAnomaly.Id).AiSource.Should().Be(DetectedAnomaly.ManualSource);

        var maintenance = history.MaintenanceHistory.Should().ContainSingle().Subject;
        maintenance.Id.Should().Be(ticket.Id);
        maintenance.AnomalyId.Should().Be(cloudAnomaly.Id);
        maintenance.Proofs.Should().ContainSingle(p => p.ProofType == "Before");
    }

    [Fact]
    public async Task GetTowerHistory_TowerOutsideUserScope_ThrowsNotFound()
    {
        var user = CreateUserMock(Guid.NewGuid(), UserRoles.Inspector);
        await using var db = CreateContext(user.Object);
        var scoped = new Region { Id = Guid.NewGuid(), Code = "IN" };
        var other = new Region { Id = Guid.NewGuid(), Code = "OUT" };
        db.Regions.AddRange(scoped, other);
        var hidden = new Tower { Id = Guid.NewGuid(), RegionId = other.Id, TowerCode = "T-HIDDEN" };
        db.Towers.Add(hidden);
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = user.Object.UserId, RegionId = scoped.Id });
        await db.SaveChangesAsync();

        var handler = new GetTowerHistoryQueryHandler(
            new TowerRepository(db, new GeographicAccessFilter(db, user.Object)),
            new AnomalyRepository(db),
            new MaintenanceTicketRepository(db),
            new InspectionMediaRepository(db));

        var act = () => handler.Handle(new GetTowerHistoryQuery(hidden.Id), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
