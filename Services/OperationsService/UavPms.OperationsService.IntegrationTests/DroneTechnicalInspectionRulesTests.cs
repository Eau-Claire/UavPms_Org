using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.Drones.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using static UavPms.OperationsService.IntegrationTests.Mf01Seed;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>UC-MF01-05 technical inspection rules (BR-22 / BR-23) saved on real PostgreSQL.</summary>
[Collection(PostgresCollection.Name)]
public class DroneTechnicalInspectionRulesTests
{
    private readonly PostgresFixture _pg;

    public DroneTechnicalInspectionRulesTests(PostgresFixture pg) => _pg = pg;

    private async Task<(Guid TechnicianId, Guid DroneId, string Tag)> SeedAsync()
    {
        await using var db = _pg.CreateContext();
        var tag = Tag();
        var technicianId = await InsertUserAsync(db, tag, UserRoles.MaintenanceTechnician);
        var droneId = await AddDroneAsync(db, tag, "INSP");
        return (technicianId, droneId, tag);
    }

    private async Task<DroneTechnicalInspection> SubmitAsync(Guid technicianId, Guid droneId, params DroneMetricSubmitDto[] metrics)
    {
        var current = Current(technicianId, UserRoles.MaintenanceTechnician);
        await using var db = _pg.CreateContext(current);
        return await new DroneTechnicalInspectionService(db, current)
            .SubmitInspectionAsync(new DroneInspectionSubmitRequest(droneId, "IT", metrics.ToList()), CancellationToken.None);
    }

    [Fact]
    public async Task KnownCriticalItem_FailsTheInspection_EvenWithoutTheCriticalFlag()
    {
        if (!_pg.Available) return;
        var (tech, drone, _) = await SeedAsync();

        // #3: MOTORS is a critical checklist item; the client did not set Critical.
        var inspection = await SubmitAsync(tech, drone,
            new DroneMetricSubmitDto("MOTORS", "Propulsion", null, BoolValue: false, Passed: false, Critical: false),
            new DroneMetricSubmitDto("BATTERY_HEALTH", "Power", 90, Unit: "%"));

        inspection.Status.Should().Be(DroneTechnicalInspectionStatus.Failed);
        inspection.Health.Should().Be(TechnicalHealth.Critical);

        await using var db = _pg.CreateContext();
        (await db.Uavs.SingleAsync(u => u.Id == drone)).TechnicalHealth.Should().Be(TechnicalHealth.Critical);
    }

    [Fact]
    public async Task EmptyMetrics_AreRejected_AndNothingIsSaved()
    {
        if (!_pg.Available) return;
        var (tech, drone, _) = await SeedAsync();

        var empty = () => SubmitAsync(tech, drone);
        await empty.Should().ThrowAsync<BusinessRuleException>().WithMessage("METRICS_REQUIRED*");

        await using var db = _pg.CreateContext();
        (await db.DroneTechnicalInspections.CountAsync(i => i.DroneId == drone)).Should().Be(0);
    }

    [Fact]
    public async Task BatteryInWarningBand_PassesWithWarning()
    {
        if (!_pg.Available) return;
        var (tech, drone, _) = await SeedAsync();

        // #11: 40 % is above the critical threshold (30) and below the warning threshold (50).
        var inspection = await SubmitAsync(tech, drone,
            new DroneMetricSubmitDto("BATTERY_HEALTH", "Power", 40, Unit: "%", Critical: true),
            new DroneMetricSubmitDto("GPS_RTK_FIX", "Avionics", null, BoolValue: true, Critical: true));

        inspection.Status.Should().Be(DroneTechnicalInspectionStatus.Passed);
        inspection.Health.Should().Be(TechnicalHealth.Warning);
    }

    [Fact]
    public async Task ItemsWithoutAReading_Fail()
    {
        if (!_pg.Available) return;
        var (tech, drone, _) = await SeedAsync();

        // #12: a required non-critical item without any reading fails (Warning) ...
        var requiredOnly = await SubmitAsync(tech, drone,
            new DroneMetricSubmitDto("CAMERA_PAYLOAD", "Payload", null, IsRequired: true, Critical: false),
            new DroneMetricSubmitDto("FRAME_BODY", "Airframe", null, BoolValue: true));
        requiredOnly.Status.Should().Be(DroneTechnicalInspectionStatus.Passed);
        requiredOnly.Health.Should().Be(TechnicalHealth.Warning);
        requiredOnly.Metrics.Single(m => m.MetricCode == "CAMERA_PAYLOAD").Passed.Should().BeFalse();

        // ... and a critical item without any reading fails the inspection.
        var critical = await SubmitAsync(tech, drone,
            new DroneMetricSubmitDto("IMU_CALIBRATION", "Sensors", null, Critical: true),
            new DroneMetricSubmitDto("FRAME_BODY", "Airframe", null, BoolValue: true));
        critical.Status.Should().Be(DroneTechnicalInspectionStatus.Failed);
        critical.Health.Should().Be(TechnicalHealth.Critical);
    }

    [Fact]
    public async Task BasicTechnicalCheck_SavesOnPostgres_WithNotesInMetadata()
    {
        if (!_pg.Available) return;
        var (tech, drone, _) = await SeedAsync();

        // #5: battery/GPS/RF items carry a numeric reading and a note; CK_DroneTechnicalMetrics_SingleValue allows one value.
        var current = Current(tech, UserRoles.MaintenanceTechnician);
        await using (var db = _pg.CreateContext(current))
        {
            var service = new UavManagementService(db, current, new DroneTechnicalInspectionService(db, current));
            var summary = await service.SubmitBasicTechnicalCheckAsync(drone, new BasicTechnicalCheckRequest
            {
                BatteryPercent = 88,
                BatteryVoltage = 24.6m,
                GpsSatelliteCount = 17,
                CommunicationConnected = true,
                BatteryNotes = "OK",
            });

            summary.Status.Should().Be("Passed");
            summary.Health.Should().Be("Healthy");
            summary.MetricCount.Should().Be(8);
        }

        await using (var db = _pg.CreateContext())
        {
            var inspection = await db.DroneTechnicalInspections.Include(i => i.Metrics)
                .Where(i => i.DroneId == drone).OrderByDescending(i => i.CompletedAt).FirstAsync();
            var battery = inspection.Metrics.Single(m => m.MetricCode == "BATTERY_HEALTH");
            battery.NumericValue.Should().Be(88);
            battery.ValueText.Should().BeNull();
            battery.Metadata.Should().Contain("24.6");

            // The note is still returned to the client.
            inspection.ToDto().Metrics.Single(m => m.MetricCode == "BATTERY_HEALTH").ValueText.Should().Contain("24.6");

            // The old mapping (numeric reading AND note in ValueText) is what the database rejects.
            db.DroneTechnicalMetrics.Add(new DroneTechnicalMetric
            {
                InspectionId = inspection.Id, MetricCode = "OLD_MAPPING", Subsystem = "Power", NumericValue = 88, ValueText = "note", Severity = "Low"
            });
            var oldMapping = () => db.SaveChangesAsync();
            (await oldMapping.Should().ThrowAsync<DbUpdateException>())
                .Which.InnerException!.Message.Should().Contain("CK_DroneTechnicalMetrics_SingleValue");
        }
    }

    [Theory]
    [InlineData(PreMissionAssessmentStatus.Cancelled, "ASSESSMENT_CANCELLED")]
    [InlineData(PreMissionAssessmentStatus.Completed, "ASSESSMENT_ALREADY_COMPLETED")]
    [InlineData(PreMissionAssessmentStatus.Expired, "ASSESSMENT_EXPIRED")]
    [InlineData(PreMissionAssessmentStatus.Ready, "ASSESSMENT_EXPIRED")] // Ready but its validity has passed
    public async Task Inspection_ForClosedAssessment_IsRejected(PreMissionAssessmentStatus status, string code)
    {
        if (!_pg.Available) return;

        Mf01Seed.Site site;
        Guid droneId, assessmentId;
        await using (var db = _pg.CreateContext())
        {
            site = await SeedSiteAsync(db);
            droneId = await AddDroneAsync(db, site.Tag, "CLOSED");
            assessmentId = await AddAssessmentWithDroneAsync(db, site, droneId, status,
                validUntil: status == PreMissionAssessmentStatus.Ready ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddHours(1));
        }

        var current = Current(site.TechnicianId, UserRoles.MaintenanceTechnician);
        await using var ctx = _pg.CreateContext(current);
        var submit = () => new DroneTechnicalInspectionService(ctx, current).SubmitInspectionAsync(
            new DroneInspectionSubmitRequest(droneId, "IT", new List<DroneMetricSubmitDto> { new("FRAME_BODY", "Airframe", null, BoolValue: true) }, PreMissionAssessmentId: assessmentId),
            CancellationToken.None);

        await submit.Should().ThrowAsync<BusinessRuleException>().WithMessage($"{code}*");
    }

    [Fact]
    public async Task Inspection_ForAssessment_ManagerNeedsRegionScope()
    {
        if (!_pg.Available) return;

        Mf01Seed.Site site;
        Guid droneId, assessmentId, outsiderManagerId;
        await using (var db = _pg.CreateContext())
        {
            site = await SeedSiteAsync(db);
            droneId = await AddDroneAsync(db, site.Tag, "SCOPE");
            assessmentId = await AddAssessmentWithDroneAsync(db, site, droneId, PreMissionAssessmentStatus.NotReady, DateTime.UtcNow.AddHours(1));
            outsiderManagerId = await InsertUserAsync(db, site.Tag, UserRoles.Manager);
        }

        var request = new DroneInspectionSubmitRequest(droneId, "IT",
            new List<DroneMetricSubmitDto> { new("FRAME_BODY", "Airframe", null, BoolValue: true) }, PreMissionAssessmentId: assessmentId);

        var outsider = Current(outsiderManagerId, UserRoles.Manager);
        await using (var db = _pg.CreateContext(outsider))
        {
            var submit = () => new DroneTechnicalInspectionService(db, outsider).SubmitInspectionAsync(request, CancellationToken.None);
            await submit.Should().ThrowAsync<ForbiddenException>().WithMessage("ASSESSMENT_ACCESS_DENIED");
        }

        var manager = Current(site.ManagerId, UserRoles.Manager);
        await using (var db = _pg.CreateContext(manager))
        {
            var inspection = await new DroneTechnicalInspectionService(db, manager).SubmitInspectionAsync(request, CancellationToken.None);
            inspection.Status.Should().Be(DroneTechnicalInspectionStatus.Passed);
        }
    }

    private static async Task<Guid> AddAssessmentWithDroneAsync(
        Infrastructure.Persistence.ApplicationDbContext db, Mf01Seed.Site site, Guid droneId, PreMissionAssessmentStatus status, DateTime validUntil)
    {
        var start = DateTime.UtcNow.AddHours(2);
        var assessment = new PreMissionAssessment
        {
            ManagerId = site.ManagerId,
            RegionId = site.RegionId,
            PlannedStart = start,
            PlannedEnd = start.AddHours(2),
            Status = status,
            ValidUntil = validUntil,
            EvaluationPolicyVersion = "v2.0",
        };
        assessment.Towers = site.TowerIds.Select((t, i) => new PreMissionAssessmentTower { AssessmentId = assessment.Id, TowerId = t, Sequence = i + 1 }).ToList();
        assessment.DroneCandidates.Add(new PreMissionAssessmentDrone { AssessmentId = assessment.Id, DroneId = droneId, SnapshotAt = DateTime.UtcNow });
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();
        return assessment.Id;
    }
}
