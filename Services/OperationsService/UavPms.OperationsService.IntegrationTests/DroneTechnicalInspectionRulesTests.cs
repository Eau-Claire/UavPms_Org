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
        var technicianId = await InsertUserAsync(db, tag, UserRoles.Technician);
        var droneId = await AddDroneAsync(db, tag, "INSP");

        // BE-REQ-101: Technician chỉ ghi được cho Drone thuộc vùng được cấp scope.
        var region = new Region { Code = $"R-{tag}", Name = $"Region {tag}", Type = "Province" };
        db.Regions.Add(region);
        (await db.Uavs.SingleAsync(u => u.Id == droneId)).AssignedRegionId = region.Id;
        await db.SaveChangesAsync();
        await AddScopeAsync(db, technicianId, region.Id);
        return (technicianId, droneId, tag);
    }

    private async Task<DroneTechnicalInspection> SubmitAsync(Guid technicianId, Guid droneId, params DroneMetricSubmitDto[] metrics)
    {
        var current = Current(technicianId, UserRoles.Technician);
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
        var current = Current(tech, UserRoles.Technician);
        await using (var db = _pg.CreateContext(current))
        {
            var service = new UavManagementService(db, current, new DroneTechnicalInspectionService(db, current));
            var summary = await service.SubmitBasicTechnicalCheckAsync(drone, new BasicTechnicalCheckRequest
            {
                FrameBodyPassed = true,
                PropellersPassed = true,
                MotorsPassed = true,
                CameraPayloadPassed = true,
                BatteryPassed = true,
                GpsPassed = true,
                CommunicationPassed = true,
                FlightControllerPassed = true,
                BatteryPercent = 88,
                BatteryVoltage = 24.6m,
                GpsSatelliteCount = 17,
                CommunicationConnected = true,
                BatteryNotes = "OK",
            });

            summary.Status.Should().Be("Passed");
            summary.Health.Should().Be("Healthy");
            // 8 hạng mục checklist + BATTERY_VOLTAGE (điện áp client gửi được giữ như số đo riêng, BRL-04).
            summary.MetricCount.Should().Be(9);
            summary.Readiness.Should().Be("Ready");
            summary.MissingMetrics.Should().BeEmpty();
        }

        await using (var db = _pg.CreateContext())
        {
            var inspection = await db.DroneTechnicalInspections.Include(i => i.Metrics)
                .Where(i => i.DroneId == drone).OrderByDescending(i => i.CompletedAt).FirstAsync();
            var battery = inspection.Metrics.Single(m => m.MetricCode == "BATTERY_HEALTH");
            battery.NumericValue.Should().Be(88);
            battery.ValueText.Should().BeNull();
            battery.Metadata.Should().Contain("OK");
            inspection.Metrics.Single(m => m.MetricCode == "BATTERY_VOLTAGE").NumericValue.Should().Be(24.6m);

            // The note is still returned to the client.
            inspection.ToDto().Metrics.Single(m => m.MetricCode == "BATTERY_HEALTH").ValueText.Should().Contain("OK");

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
}
