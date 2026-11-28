using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// Các truy vấn lọc theo vùng / nhiệm vụ (MissionScope, DroneScope, RegionScope) phải dịch được sang SQL thật
/// (subquery, Union, Contains lồng nhau) và trả đúng dữ liệu trên PostgreSQL.
/// </summary>
[Collection(PostgresCollection.Name)]
public class RegionVisibilityQueryTests
{
    private readonly PostgresFixture _pg;

    public RegionVisibilityQueryTests(PostgresFixture pg) => _pg = pg;

    /// <summary>Một nhiệm vụ có ảnh, defect Confirmed, cảnh báo Open và một Drone, tất cả thuộc vùng của site.</summary>
    private static async Task<(Guid MissionId, Guid DroneId)> SeedMissionDataAsync(ApplicationDbContext db, Mf01Seed.Site site, int categoryId)
    {
        var droneId = await Mf01Seed.AddDroneAsync(db, site.Tag, "VIS", regionId: site.RegionId);
        var mission = new Mission
        {
            Id = Guid.NewGuid(), MissionCode = $"MS-VIS-{site.Tag}", Title = "Visibility", ManagerId = site.ManagerId,
            RegionId = site.RegionId, Status = MissionStatus.Completed
        };
        var media = new InspectionMedia
        {
            Id = Guid.NewGuid(), MissionId = mission.Id, TowerId = site.TowerIds[0], MediaType = "Image",
            FileUrl = "uav-media/vis.jpg", ValidationStatus = "Confirmed", CapturedAt = DateTime.UtcNow
        };
        var anomaly = new DetectedAnomaly
        {
            Id = Guid.NewGuid(), MediaId = media.Id, CategoryId = categoryId, TowerId = site.TowerIds[0],
            BoundingBox = "{}", ConfidenceScore = 0.9, ValidationStatus = "Confirmed", AnalystNotes = ""
        };
        db.AddRange(mission, media, anomaly,
            new EmergencyAlert { Id = Guid.NewGuid(), AnomalyId = anomaly.Id, Status = "Open", Priority = "Critical" });
        await db.SaveChangesAsync();
        return (mission.Id, droneId);
    }

    [Fact]
    public async Task DashboardAlertsAndDrones_AreLimitedToTheManagersRegion()
    {
        if (!_pg.Available) return;

        Mf01Seed.Site own, other;
        Guid ownMission, ownDrone, otherDrone;
        await using (var db = _pg.CreateContext())
        {
            own = await Mf01Seed.SeedSiteAsync(db);
            other = await Mf01Seed.SeedSiteAsync(db);
            var category = new DefectCategory { CategoryCode = $"VIS-{own.Tag}", CategoryName = "Visibility", Description = "", SeverityWeight = 1, IsEmergencyClass = true };
            db.DefectCategories.Add(category);
            await db.SaveChangesAsync();
            (ownMission, ownDrone) = await SeedMissionDataAsync(db, own, category.Id);
            (_, otherDrone) = await SeedMissionDataAsync(db, other, category.Id);
        }

        var manager = Mf01Seed.Current(own.ManagerId, UserRoles.Manager);
        await using (var db = _pg.CreateContext(manager))
        {
            var summary = await new MonitorRepository(db, manager).GetSummaryAsync(CancellationToken.None);
            summary.TotalMissions.Should().Be(1);
            summary.TotalInspections.Should().Be(1);
            summary.TotalDefects.Should().Be(1);
            var (recent, _) = await new MonitorRepository(db, manager).GetRecentDefectsAsync(1, 10, CancellationToken.None);
            recent.Select(r => r.MissionId).Should().Equal(ownMission);

            var alerts = await new EmergencyAlertService(db, Mock.Of<IEventPublisher>(), NullLogger<EmergencyAlertService>.Instance, manager)
                .GetActiveAlertsAsync();
            alerts.Should().ContainSingle();

            var drones = await new UavRepository(db).GetVisibleAsync();
            drones.Select(d => d.Id).Should().Contain(ownDrone).And.NotContain(otherDrone);
        }

        var admin = Mf01Seed.Current(Guid.NewGuid(), UserRoles.SystemAdmin);
        await using (var db = _pg.CreateContext(admin))
        {
            var drones = await new UavRepository(db).GetVisibleAsync();
            drones.Select(d => d.Id).Should().Contain(new[] { ownDrone, otherDrone });
            (await new MonitorRepository(db, admin).GetSummaryAsync(CancellationToken.None)).TotalMissions.Should().BeGreaterThanOrEqualTo(2);
        }
    }
}
