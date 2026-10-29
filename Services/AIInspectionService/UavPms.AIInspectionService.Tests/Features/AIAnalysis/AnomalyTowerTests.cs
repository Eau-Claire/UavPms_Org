using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Features.Anomalies;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.AIInspectionService.Infrastructure.Persistence;
using UavPms.AIInspectionService.Infrastructure.Repositories;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

public class AnomalyTowerTests
{
    // Hoan Kiem, Ha Noi. 1e-3 deg of latitude ≈ 111 m; of longitude ≈ 104 m at this latitude.
    private const double Lat = 21.0285;
    private const double Lng = 105.8542;

    private static Point P(double lat, double lng) => new(lng, lat) { SRID = 4326 };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, Mock.Of<ICurrentUserServices>());
    }

    private static async Task<(DetectedAnomaly Anomaly, InspectionMedia Media)> SeedAnomalyAsync(
        ApplicationDbContext db, Point? anomalyLocation, Point? captureLocation, string status = "Pending")
    {
        // GetByIdWithDetailAsync includes the required Category navigation.
        var category = new DefectCategory { Id = 7, CategoryCode = "CI", CategoryName = "Corroded insulator" };
        var media = new InspectionMedia { Id = Guid.NewGuid(), MissionId = Guid.NewGuid(), FileUrl = "/v.mp4", MediaType = "Video", CaptureLocation = captureLocation };
        var anomaly = new DetectedAnomaly { Id = Guid.NewGuid(), MediaId = media.Id, CategoryId = category.Id, Location = anomalyLocation, ValidationStatus = status, BoundingBox = "{}" };
        db.DefectCategories.Add(category);
        db.InspectionMedia.Add(media);
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();
        return (anomaly, media);
    }

    private static Tower NewTower(string code, double lat, double lng) =>
        new() { Id = Guid.NewGuid(), RegionId = Guid.NewGuid(), TowerCode = code, Geom = P(lat, lng) };

    [Fact]
    public async Task Suggestions_ReturnTowersWithinRadius_ClosestFirst()
    {
        await using var db = CreateContext();
        var near = NewTower("NEAR", Lat + 0.0005, Lng);        // ~56 m north
        var mid = NewTower("MID", Lat, Lng + 0.0024);          // ~250 m east
        var far = NewTower("FAR", Lat + 0.02, Lng);            // ~2.2 km
        db.Towers.AddRange(far, mid, near);
        var (anomaly, _) = await SeedAnomalyAsync(db, P(Lat, Lng), null);
        anomaly.TowerId = mid.Id;
        await db.SaveChangesAsync();

        var result = await new GetAnomalyTowerSuggestionsQueryHandler(new AnomalyRepository(db), new GenericRepository<Tower>(db))
            .Handle(new GetAnomalyTowerSuggestionsQuery(anomaly.Id, RadiusMeters: 300), CancellationToken.None);

        result.Source.Should().Be("Anomaly");
        result.CurrentTowerId.Should().Be(mid.Id);
        result.Towers.Select(t => t.TowerCode).Should().Equal("NEAR", "MID");
        result.Towers[0].DistanceMeters.Should().BeApproximately(55.6, 1.0);
        result.Towers[1].DistanceMeters.Should().BeApproximately(249.3, 2.0);
        result.Towers.Single(t => t.TowerCode == "MID").IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task Suggestions_FallBackToMediaCaptureLocation_AndRespectLimit()
    {
        await using var db = CreateContext();
        db.Towers.AddRange(NewTower("A", Lat + 0.0001, Lng), NewTower("B", Lat + 0.0002, Lng), NewTower("C", Lat + 0.0003, Lng));
        var (anomaly, _) = await SeedAnomalyAsync(db, null, P(Lat, Lng));

        var result = await new GetAnomalyTowerSuggestionsQueryHandler(new AnomalyRepository(db), new GenericRepository<Tower>(db))
            .Handle(new GetAnomalyTowerSuggestionsQuery(anomaly.Id, Limit: 2), CancellationToken.None);

        result.Source.Should().Be("Media");
        result.Towers.Select(t => t.TowerCode).Should().Equal("A", "B");
    }

    [Fact]
    public async Task Suggestions_WithoutAnyLocation_ThrowLocationUnknown()
    {
        await using var db = CreateContext();
        var (anomaly, _) = await SeedAnomalyAsync(db, null, null);

        var act = () => new GetAnomalyTowerSuggestionsQueryHandler(new AnomalyRepository(db), new GenericRepository<Tower>(db))
            .Handle(new GetAnomalyTowerSuggestionsQuery(anomaly.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("LOCATION_UNKNOWN");
    }

    [Fact]
    public async Task Assign_SetsTowerOnPendingAnomaly()
    {
        await using var db = CreateContext();
        var tower = NewTower("T-1", Lat, Lng);
        db.Towers.Add(tower);
        var (anomaly, _) = await SeedAnomalyAsync(db, null, null);

        var result = await new AssignAnomalyTowerCommandHandler(new AnomalyRepository(db), new GenericRepository<Tower>(db), new UnitOfWork(db))
            .Handle(new AssignAnomalyTowerCommand(anomaly.Id, tower.Id), CancellationToken.None);

        result.Should().Be(new AnomalyTowerAssignmentDto(anomaly.Id, tower.Id, "T-1", "Pending"));
        db.ChangeTracker.Clear();
        (await db.DetectedAnomalies.SingleAsync(a => a.Id == anomaly.Id)).TowerId.Should().Be(tower.Id);
    }

    [Theory]
    [InlineData("Resolved", false, "ANOMALY_RESOLVED")]
    [InlineData("Pending", true, "ANOMALY_SUPERSEDED")]
    public async Task Assign_RejectsResolvedOrSupersededAnomaly(string status, bool superseded, string expectedCode)
    {
        await using var db = CreateContext();
        var tower = NewTower("T-1", Lat, Lng);
        db.Towers.Add(tower);
        var (anomaly, _) = await SeedAnomalyAsync(db, null, null, status);
        if (superseded)
        {
            anomaly.SupersededById = Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        var act = () => new AssignAnomalyTowerCommandHandler(new AnomalyRepository(db), new GenericRepository<Tower>(db), new UnitOfWork(db))
            .Handle(new AssignAnomalyTowerCommand(anomaly.Id, tower.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task Assign_UnknownTower_ThrowsNotFound()
    {
        await using var db = CreateContext();
        var (anomaly, _) = await SeedAnomalyAsync(db, null, null);

        var act = () => new AssignAnomalyTowerCommandHandler(new AnomalyRepository(db), new GenericRepository<Tower>(db), new UnitOfWork(db))
            .Handle(new AssignAnomalyTowerCommand(anomaly.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
