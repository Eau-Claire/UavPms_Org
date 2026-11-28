using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Alerts.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Security;

/// <summary>
/// Spec §4: dashboard, cảnh báo khẩn và Drone chỉ hiện dữ liệu của nhiệm vụ / vùng người gọi được xem
/// (SystemAdmin thấy tất cả). Dữ liệu mẫu: một nhiệm vụ ở vùng của người gọi và một nhiệm vụ ở vùng khác,
/// mỗi nhiệm vụ có một ảnh, một defect Confirmed, một cảnh báo Open và một Drone.
/// </summary>
public class RegionVisibilityTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _ownRegion = Guid.NewGuid();
    private readonly Guid _otherRegion = Guid.NewGuid();
    private readonly Guid _ownMission = Guid.NewGuid();
    private readonly Guid _otherMission = Guid.NewGuid();
    private readonly Guid _ownDrone = Guid.NewGuid();
    private readonly Guid _otherDrone = Guid.NewGuid();

    private static ICurrentUserServices User(Guid userId, string role)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        return mock.Object;
    }

    private async Task<ApplicationDbContext> SeedAsync(ICurrentUserServices user)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, user);
        var category = new DefectCategory { Id = 1, CategoryCode = "BIRD_NEST", CategoryName = "Bird nest", IsEmergencyClass = true };
        db.DefectCategories.Add(category);
        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = _userId, RegionId = _ownRegion });

        foreach (var (missionId, regionId, droneId) in new[] { (_ownMission, _ownRegion, _ownDrone), (_otherMission, _otherRegion, _otherDrone) })
        {
            db.Uavs.Add(new Uav { Id = droneId, UavCode = droneId.ToString("N")[..8], Name = "Drone", Model = "M300", AssignedRegionId = regionId });
            db.Missions.Add(new Mission { Id = missionId, MissionCode = missionId.ToString("N")[..8], Title = "M", RegionId = regionId, Status = MissionStatus.Completed });
            var media = new InspectionMedia { Id = Guid.NewGuid(), MissionId = missionId, MediaType = "Image", FileUrl = "x", ValidationStatus = "Confirmed" };
            var anomaly = new DetectedAnomaly { Id = Guid.NewGuid(), MediaId = media.Id, Media = media, CategoryId = category.Id, BoundingBox = "{}", ValidationStatus = "Confirmed" };
            db.InspectionMedia.Add(media);
            db.DetectedAnomalies.Add(anomaly);
            db.EmergencyAlerts.Add(new EmergencyAlert { Id = Guid.NewGuid(), AnomalyId = anomaly.Id, Anomaly = anomaly, Status = "Open", Priority = "Critical" });
        }
        await db.SaveChangesAsync();
        return db;
    }

    private EmergencyAlertService Alerts(ApplicationDbContext db, ICurrentUserServices user) =>
        new(db, Mock.Of<IEventPublisher>(), NullLogger<EmergencyAlertService>.Instance, user);

    [Fact]
    public async Task Dashboard_CountsOnlyVisibleMissions_SystemAdminSeesAll()
    {
        var manager = User(_userId, UserRoles.Manager);
        await using var db = await SeedAsync(manager);

        var managerSummary = await new MonitorRepository(db, manager).GetSummaryAsync(CancellationToken.None);
        var (recent, _) = await new MonitorRepository(db, manager).GetRecentDefectsAsync(1, 10, CancellationToken.None);
        var adminSummary = await new MonitorRepository(db, User(Guid.NewGuid(), UserRoles.SystemAdmin)).GetSummaryAsync(CancellationToken.None);

        managerSummary.TotalMissions.Should().Be(1);
        managerSummary.TotalInspections.Should().Be(1);
        managerSummary.TotalDefects.Should().Be(1);
        recent.Select(r => r.MissionId).Should().Equal(_ownMission);
        adminSummary.TotalMissions.Should().Be(2);
        adminSummary.TotalDefects.Should().Be(2);
    }

    [Fact]
    public async Task Dashboard_FieldMemberWithoutRegion_SeesOnlyAssignedMission()
    {
        var inspectorId = Guid.NewGuid();
        var inspector = User(inspectorId, UserRoles.Inspector);
        await using var db = await SeedAsync(inspector);
        db.MissionAssignments.Add(new MissionAssignment { Id = Guid.NewGuid(), MissionId = _otherMission, UserId = inspectorId, AssignmentRole = MissionAssignment.InspectorRole });
        await db.SaveChangesAsync();

        var summary = await new MonitorRepository(db, inspector).GetSummaryAsync(CancellationToken.None);

        summary.TotalMissions.Should().Be(1);
        summary.TotalDefects.Should().Be(1);
    }

    [Fact]
    public async Task Alerts_ListAndDetail_OnlyVisibleMissions()
    {
        var manager = User(_userId, UserRoles.Manager);
        await using var db = await SeedAsync(manager);
        var service = Alerts(db, manager);
        var otherAlertId = await db.EmergencyAlerts.Where(a => a.Anomaly!.Media!.MissionId == _otherMission).Select(a => a.Id).SingleAsync();

        (await service.GetActiveAlertsAsync()).Should().ContainSingle();
        (await service.GetAlertHistoryAsync(null, 1, 10)).TotalCount.Should().Be(1);
        (await service.GetAlertByIdAsync(otherAlertId)).Should().BeNull();
        (await service.ReviewAlertAsync(otherAlertId, _userId, new ReviewEmergencyAlertRequest { Status = "Acknowledged" })).Should().BeFalse();
        (await Alerts(db, User(Guid.NewGuid(), UserRoles.SystemAdmin)).GetActiveAlertsAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task TriggerAlert_RequiresVisibleMission_AndNeverFallsBackToLatestMission()
    {
        var manager = User(_userId, UserRoles.Manager);
        await using var db = await SeedAsync(manager);
        var service = Alerts(db, manager);

        var withoutMission = () => service.TriggerAlertAsync(new TriggerEmergencyAlertRequest());
        var otherRegion = () => service.TriggerAlertAsync(new TriggerEmergencyAlertRequest { MissionId = _otherMission });

        await withoutMission.Should().ThrowAsync<BusinessRuleException>().WithMessage("*MISSION_REQUIRED*");
        await otherRegion.Should().ThrowAsync<ForbiddenException>().WithMessage("MISSION_ACCESS_DENIED");
    }

    [Fact]
    public async Task Drones_VisibleByRegionOrAssignedMission()
    {
        var inspectorId = Guid.NewGuid();
        var inspector = User(inspectorId, UserRoles.Inspector);
        await using var db = await SeedAsync(inspector);
        var repository = new UavRepository(db);

        (await repository.GetVisibleAsync()).Should().BeEmpty();

        var mission = await db.Missions.SingleAsync(m => m.Id == _otherMission);
        mission.UavId = _otherDrone;
        db.MissionAssignments.Add(new MissionAssignment { Id = Guid.NewGuid(), MissionId = _otherMission, UserId = inspectorId, AssignmentRole = MissionAssignment.InspectorRole });
        await db.SaveChangesAsync();

        (await repository.GetVisibleAsync()).Select(u => u.Id).Should().Equal(_otherDrone);
        (await repository.IsVisibleAsync(_ownDrone)).Should().BeFalse();
    }
}
