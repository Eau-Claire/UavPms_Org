using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Enums;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// BE-REQ-101 Fix vòng 2 (Codex Auditor): /drones/eligible theo scope, /re-evaluate tự loại Drone (BRL-03),
/// idempotency đối chiếu droneIds + headcount, basic-technical-check lưu connected dạng boolean (BRL-04),
/// khóa Flight Controller và không bịa firmware (BRL-02).
/// </summary>
public sealed class Be101AuditFixR2Tests : IDisposable
{
    private const string Mf01Url = "/api/v1/pre-mission-assessments";

    private readonly Be101World _world = new();

    public void Dispose() => _world.Dispose();

    private async Task<Guid> CreateMf01Async(HttpClient client, Be101World.RegionSeed region, params Guid[] droneIds)
    {
        var response = await Be101World.PostJsonAsync(client, Mf01Url, Be101World.Mf01Payload(region, droneIds: droneIds));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await Be101World.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<List<Guid>> CandidateIdsAsync(HttpResponseMessage response) =>
        (await Be101World.ReadJsonAsync(response)).GetProperty("droneCandidates").EnumerateArray()
            .Select(c => c.GetProperty("droneId").GetGuid())
            .ToList();

    private async Task<List<Guid>> StoredDroneIdsAsync(Guid assessmentId)
    {
        var findings = await _world.WithDbAsync(async db =>
            (await db.PreMissionAssessments.SingleAsync(a => a.Id == assessmentId)).Findings);
        using var doc = JsonDocument.Parse(findings);
        return doc.RootElement.TryGetProperty("droneIds", out var ids)
            ? ids.EnumerateArray().Select(i => i.GetGuid()).ToList()
            : new List<Guid>();
    }
    // ------------------------------------------------ 1. GET /drones/eligible lọc theo quyền và scope

    [Fact]
    public async Task Eligible_Analyst_IsForbidden()
    {
        var region = await _world.SeedRegionAsync();
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, region.RegionId);
        var assessmentId = await CreateMf01Async(_world.Client(managerId, UserRoles.Manager), region);
        var analystId = await _world.SeedUserAsync(new[] { UserRoles.Analyst }, region.RegionId);

        var response = await _world.Client(analystId, UserRoles.Analyst).GetAsync($"/api/v1/drones/eligible?assessmentId={assessmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Eligible_ManagerOutsideTheAssessmentRegionScope_IsRejected()
    {
        var regionA = await _world.SeedRegionAsync();
        var regionB = await _world.SeedRegionAsync();
        await _world.SeedDroneAsync(regionA.RegionId, TechnicalHealth.Healthy);
        var managerOfA = await _world.SeedUserAsync(new[] { UserRoles.Manager }, regionA.RegionId);
        var assessmentId = await CreateMf01Async(_world.Client(managerOfA, UserRoles.Manager), regionA);
        var managerOfB = await _world.SeedUserAsync(new[] { UserRoles.Manager }, regionB.RegionId);

        var response = await _world.Client(managerOfB, UserRoles.Manager).GetAsync($"/api/v1/drones/eligible?assessmentId={assessmentId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(UserRoles.Manager)]
    [InlineData(UserRoles.SystemAdmin)]
    public async Task Eligible_AllowedRole_ReturnsOnlyDronesOfTheAssessmentRegion(string role)
    {
        var regionA = await _world.SeedRegionAsync();
        var regionB = await _world.SeedRegionAsync();
        var droneOfA = await _world.SeedDroneAsync(regionA.RegionId, TechnicalHealth.Healthy);
        var droneOfB = await _world.SeedDroneAsync(regionB.RegionId, TechnicalHealth.Healthy);
        var managerOfA = await _world.SeedUserAsync(new[] { UserRoles.Manager }, regionA.RegionId);
        var assessmentId = await CreateMf01Async(_world.Client(managerOfA, UserRoles.Manager), regionA);
        var callerId = role == UserRoles.SystemAdmin
            ? await _world.SeedUserAsync(new[] { role })
            : managerOfA;

        var response = await _world.Client(callerId, role).GetAsync($"/api/v1/drones/eligible?assessmentId={assessmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var droneIds = (await Be101World.ReadJsonAsync(response)).EnumerateArray()
            .Select(d => d.GetProperty("droneId").GetGuid())
            .ToList();
        droneIds.Should().Contain(droneOfA);
        droneIds.Should().NotContain(droneOfB);
    }

    // ------------------------------------------------ 2. BRL-03: /re-evaluate tự loại Drone

    [Fact]
    public async Task ReEvaluate_WithoutDroneIds_PrunesDroneMovedToAnotherRegion()
    {
        var regionA = await _world.SeedRegionAsync();
        var regionB = await _world.SeedRegionAsync();
        var keptDrone = await _world.SeedDroneAsync(regionA.RegionId, TechnicalHealth.Healthy);
        var movedDrone = await _world.SeedDroneAsync(regionA.RegionId, TechnicalHealth.Healthy);
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, regionA.RegionId);
        var client = _world.Client(managerId, UserRoles.Manager);
        var assessmentId = await CreateMf01Async(client, regionA, keptDrone, movedDrone);

        await _world.WithDbAsync(async db =>
        {
            (await db.Uavs.SingleAsync(u => u.Id == movedDrone)).AssignedRegionId = regionB.RegionId;
            await db.SaveChangesAsync();
        });
        var response = await Be101World.PostJsonAsync(client, $"{Mf01Url}/{assessmentId}/re-evaluate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await CandidateIdsAsync(response)).Should().Equal(keptDrone);
        (await StoredDroneIdsAsync(assessmentId)).Should().Equal(keptDrone);
    }

    [Fact]
    public async Task ReEvaluate_WithoutDroneIds_PrunesDroneThatIsNoLongerReady()
    {
        var region = await _world.SeedRegionAsync();
        var keptDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var brokenDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, region.RegionId);
        var client = _world.Client(managerId, UserRoles.Manager);
        var assessmentId = await CreateMf01Async(client, region, keptDrone, brokenDrone);

        await _world.WithDbAsync(async db =>
        {
            (await db.Uavs.SingleAsync(u => u.Id == brokenDrone)).TechnicalHealth = TechnicalHealth.Critical;
            await db.SaveChangesAsync();
        });
        var response = await Be101World.PostJsonAsync(client, $"{Mf01Url}/{assessmentId}/re-evaluate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await CandidateIdsAsync(response)).Should().Equal(keptDrone);
        (await StoredDroneIdsAsync(assessmentId)).Should().Equal(keptDrone);
    }

    [Fact]
    public async Task ReEvaluate_WhenEverySelectedDroneIsPruned_DoesNotFallBackToAllRegionDrones()
    {
        var regionA = await _world.SeedRegionAsync();
        var regionB = await _world.SeedRegionAsync();
        var selectedDrone = await _world.SeedDroneAsync(regionA.RegionId, TechnicalHealth.Healthy);
        await _world.SeedDroneAsync(regionA.RegionId, TechnicalHealth.Healthy); // Ready cùng vùng nhưng không được chọn
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, regionA.RegionId);
        var client = _world.Client(managerId, UserRoles.Manager);
        var assessmentId = await CreateMf01Async(client, regionA, selectedDrone);

        await _world.WithDbAsync(async db =>
        {
            (await db.Uavs.SingleAsync(u => u.Id == selectedDrone)).AssignedRegionId = regionB.RegionId;
            await db.SaveChangesAsync();
        });
        var response = await Be101World.PostJsonAsync(client, $"{Mf01Url}/{assessmentId}/re-evaluate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await CandidateIdsAsync(response)).Should().BeEmpty();
        (await StoredDroneIdsAsync(assessmentId)).Should().BeEmpty();
    }

    [Fact]
    public async Task ReEvaluate_WithNewDroneIdsThatAreNotReady_IsStillRejected409()
    {
        var region = await _world.SeedRegionAsync();
        var readyDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var warningDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Warning);
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, region.RegionId);
        var client = _world.Client(managerId, UserRoles.Manager);
        var assessmentId = await CreateMf01Async(client, region, readyDrone);

        var response = await Be101World.PostJsonAsync(
            client, $"{Mf01Url}/{assessmentId}/re-evaluate", new { droneIds = new[] { warningDrone } });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(response)).GetProperty("errorCode").GetString().Should().Be("DRONE_NOT_READY");
    }

    // ------------------------------------------------ 3. Idempotency đối chiếu droneIds và headcount

    private static object IdempotentPayload(
        Be101World.RegionSeed region, DateTime plannedStart, string key, int inspectorCount, params Guid[] droneIds) =>
        new Dictionary<string, object?>
        {
            ["regionId"] = region.RegionId,
            ["plannedStart"] = plannedStart,
            ["plannedEnd"] = plannedStart.AddHours(4),
            ["towerIds"] = region.TowerIds,
            ["inspectorCount"] = inspectorCount,
            ["analystCount"] = 1,
            ["technicianCount"] = 1,
            ["droneIds"] = droneIds,
            ["idempotencyKey"] = key
        };

    private static DateTime FixedPlannedStart()
    {
        var start = DateTime.UtcNow.AddDays(1);
        return new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0, DateTimeKind.Utc);
    }

    [Fact]
    public async Task Idempotency_ReplayOfTheSamePayload_ReturnsTheSameAssessment()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, region.RegionId);
        var client = _world.Client(managerId, UserRoles.Manager);
        var start = FixedPlannedStart();

        var first = await Be101World.PostJsonAsync(client, Mf01Url, IdempotentPayload(region, start, "key-r2-same", 1, droneId));
        var replay = await Be101World.PostJsonAsync(client, Mf01Url, IdempotentPayload(region, start, "key-r2-same", 1, droneId));

        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        replay.StatusCode.Should().Be(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(replay)).GetProperty("id").GetGuid()
            .Should().Be((await Be101World.ReadJsonAsync(first)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Idempotency_ReplayWithDifferentDroneIds_Returns409KeyReused()
    {
        var region = await _world.SeedRegionAsync();
        var firstDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var otherDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, region.RegionId);
        var client = _world.Client(managerId, UserRoles.Manager);
        var start = FixedPlannedStart();

        (await Be101World.PostJsonAsync(client, Mf01Url, IdempotentPayload(region, start, "key-r2-drone", 1, firstDrone)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var replay = await Be101World.PostJsonAsync(client, Mf01Url, IdempotentPayload(region, start, "key-r2-drone", 1, otherDrone));

        replay.StatusCode.Should().Be(HttpStatusCode.Conflict, await replay.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(replay)).GetProperty("errorCode").GetString().Should().Be("IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task Idempotency_ReplayWithDifferentHeadcount_Returns409KeyReused()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, region.RegionId);
        var client = _world.Client(managerId, UserRoles.Manager);
        var start = FixedPlannedStart();

        (await Be101World.PostJsonAsync(client, Mf01Url, IdempotentPayload(region, start, "key-r2-count", 1, droneId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var replay = await Be101World.PostJsonAsync(client, Mf01Url, IdempotentPayload(region, start, "key-r2-count", 2, droneId));

        replay.StatusCode.Should().Be(HttpStatusCode.Conflict, await replay.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(replay)).GetProperty("errorCode").GetString().Should().Be("IDEMPOTENCY_KEY_REUSED");
    }

    // ------------------------------------------------ 4. BRL-04: basic-technical-check

    private async Task<(HttpResponseMessage Response, Guid DroneId)> PostBasicCheckAsync(object body)
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, region.RegionId);
        var response = await Be101World.PostJsonAsync(
            _world.Client(technicianId, UserRoles.Technician), $"/api/v1/drones/{droneId}/basic-technical-check", body);
        return (response, droneId);
    }

    [Fact]
    public async Task BasicCheck_OnlyVoltageAndCells_KeepsBothReadingsAndFlagsMissingItems()
    {
        var (response, droneId) = await PostBasicCheckAsync(new { batteryVoltage = 24.1, batteryCellCount = 6 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(response);
        body.GetProperty("status").GetString().Should().Be("Incomplete");
        body.GetProperty("readiness").GetString().Should().Be("MaintenanceRequired");
        body.GetProperty("missingMetrics").EnumerateArray().Select(m => m.GetString())
            .Should().Contain(new[] { "FRAME_BODY", "BATTERY_HEALTH", "FLIGHT_CONTROLLER" });

        var inspection = await _world.WithDbAsync(db =>
            db.DroneTechnicalInspections.Include(i => i.Metrics).SingleAsync(i => i.DroneId == droneId));
        inspection.Metrics.Single(m => m.MetricCode == "BATTERY_VOLTAGE").NumericValue.Should().Be(24.1m);
        inspection.Metrics.Single(m => m.MetricCode == "BATTERY_CELL_COUNT").NumericValue.Should().Be(6m);
        var health = await _world.WithDbAsync(async db => (await db.Uavs.SingleAsync(u => u.Id == droneId)).TechnicalHealth);
        health.Should().NotBe(TechnicalHealth.Healthy);
    }

    [Fact]
    public async Task BasicCheck_Disconnected_IsStoredAsBoolean_WithoutAnInventedZeroPercent()
    {
        var (response, droneId) = await PostBasicCheckAsync(new { communicationConnected = false, batteryPercent = 70 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(response)).GetProperty("readiness").GetString().Should().Be("MaintenanceRequired");

        var inspection = await _world.WithDbAsync(db =>
            db.DroneTechnicalInspections.Include(i => i.Metrics).SingleAsync(i => i.DroneId == droneId));
        var link = inspection.Metrics.Single(m => m.MetricCode == "RF_LINK_TELEMETRY");
        link.BoolValue.Should().BeFalse();
        link.NumericValue.Should().BeNull();
        link.Passed.Should().BeFalse();
        inspection.Metrics.Single(m => m.MetricCode == "BATTERY_HEALTH").NumericValue.Should().Be(70m);
    }

    [Fact]
    public async Task BasicCheck_CompleteChecklist_IsPassedAndReady()
    {
        var (response, droneId) = await PostBasicCheckAsync(new
        {
            frameBodyPassed = true,
            propellersPassed = true,
            motorsPassed = true,
            batteryPassed = true,
            batteryPercent = 90,
            gpsPassed = true,
            gpsSatelliteCount = 14,
            communicationConnected = true,
            flightControllerPassed = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(response);
        body.GetProperty("status").GetString().Should().Be("Passed");
        body.GetProperty("readiness").GetString().Should().Be("Ready");
        body.GetProperty("missingMetrics").GetArrayLength().Should().Be(0);
        var health = await _world.WithDbAsync(async db => (await db.Uavs.SingleAsync(u => u.Id == droneId)).TechnicalHealth);
        health.Should().Be(TechnicalHealth.Healthy);
    }

    // ------------------------------------------------ 5. BRL-02: khóa Flight Controller, không bịa firmware

    private const string FcUid = "FC-R2-0001";

    [Theory]
    [InlineData(UserRoles.Technician)]
    [InlineData(UserRoles.SystemAdmin)]
    public async Task Pair_UidActiveOnAnotherDrone_Returns409_AndKeepsTheOldBinding(string role)
    {
        var region = await _world.SeedRegionAsync();
        var oldDrone = await _world.SeedDroneAsync(region.RegionId);
        var newDrone = await _world.SeedDroneAsync(region.RegionId);
        await _world.SeedFlightControllerBindingAsync(oldDrone, FcUid, DateTime.UtcNow.AddDays(-2), active: true);
        var userId = role == UserRoles.SystemAdmin
            ? await _world.SeedUserAsync(new[] { role })
            : await _world.SeedUserAsync(new[] { role }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(userId, role), $"/api/v1/drones/{newDrone}/flight-controller", new { flightControllerUid = FcUid });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(response)).GetProperty("errorCode").GetString().Should().Be("FC_ALREADY_PAIRED");
        var bindings = await _world.WithDbAsync(db => db.DroneFlightControllers.ToListAsync());
        bindings.Should().ContainSingle(b => b.DroneId == oldDrone && b.UnpairedAt == null);
        bindings.Should().NotContain(b => b.DroneId == newDrone);
    }

    [Fact]
    public async Task Pair_AfterDisconnectingTheOldDrone_Succeeds()
    {
        var region = await _world.SeedRegionAsync();
        var oldDrone = await _world.SeedDroneAsync(region.RegionId);
        var newDrone = await _world.SeedDroneAsync(region.RegionId);
        await _world.SeedFlightControllerBindingAsync(oldDrone, FcUid, DateTime.UtcNow.AddDays(-2), active: true);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, region.RegionId);
        var client = _world.Client(technicianId, UserRoles.Technician);

        var unpair = await client.DeleteAsync($"/api/v1/drones/{oldDrone}/flight-controller");
        unpair.StatusCode.Should().Be(HttpStatusCode.OK, await unpair.Content.ReadAsStringAsync());
        var pair = await Be101World.PostJsonAsync(client, $"/api/v1/drones/{newDrone}/flight-controller", new { flightControllerUid = FcUid });

        pair.StatusCode.Should().Be(HttpStatusCode.OK, await pair.Content.ReadAsStringAsync());
        var active = await _world.WithDbAsync(db => db.DroneFlightControllers.Where(b => b.UnpairedAt == null).ToListAsync());
        active.Should().ContainSingle(b => b.DroneId == newDrone);
    }

    [Fact]
    public async Task Pair_WithoutFirmware_LeavesFirmwareEmpty_AndKeepsWhatWasSent()
    {
        var region = await _world.SeedRegionAsync();
        var bareDrone = await _world.SeedDroneAsync(region.RegionId);
        var fullDrone = await _world.SeedDroneAsync(region.RegionId);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, region.RegionId);
        var client = _world.Client(technicianId, UserRoles.Technician);

        var bare = await Be101World.PostJsonAsync(client, $"/api/v1/drones/{bareDrone}/flight-controller", new { flightControllerUid = "FC-R2-BARE" });
        var full = await Be101World.PostJsonAsync(client, $"/api/v1/drones/{fullDrone}/flight-controller",
            new { flightControllerUid = "FC-R2-FULL", firmwareName = "ArduCopter", firmwareVersion = "4.5.1" });

        bare.StatusCode.Should().Be(HttpStatusCode.OK, await bare.Content.ReadAsStringAsync());
        var bareBody = await Be101World.ReadJsonAsync(bare);
        bareBody.GetProperty("firmwareName").ValueKind.Should().Be(JsonValueKind.Null);
        bareBody.GetProperty("firmwareVersion").ValueKind.Should().Be(JsonValueKind.Null);

        full.StatusCode.Should().Be(HttpStatusCode.OK, await full.Content.ReadAsStringAsync());
        var bindings = await _world.WithDbAsync(db => db.DroneFlightControllers.ToListAsync());
        bindings.Single(b => b.DroneId == bareDrone).FirmwareName.Should().BeNull();
        bindings.Single(b => b.DroneId == fullDrone).FirmwareName.Should().Be("ArduCopter");
        bindings.Single(b => b.DroneId == fullDrone).FirmwareVersion.Should().Be("4.5.1");
    }

    [Fact]
    public async Task PairAndUnpair_TechnicianOutsideTheDroneRegion_IsRejected_AndNothingChanges()
    {
        var regionOfUser = await _world.SeedRegionAsync();
        var regionOfDrone = await _world.SeedRegionAsync();
        var drone = await _world.SeedDroneAsync(regionOfDrone.RegionId);
        await _world.SeedFlightControllerBindingAsync(drone, FcUid, DateTime.UtcNow.AddDays(-2), active: true);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, regionOfUser.RegionId);
        var client = _world.Client(technicianId, UserRoles.Technician);

        var pair = await Be101World.PostJsonAsync(client, $"/api/v1/drones/{drone}/flight-controller", new { flightControllerUid = "FC-R2-OTHER" });
        var unpair = await client.DeleteAsync($"/api/v1/drones/{drone}/flight-controller");

        pair.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        unpair.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        var bindings = await _world.WithDbAsync(db => db.DroneFlightControllers.ToListAsync());
        bindings.Should().ContainSingle(b => b.FlightControllerUid == FcUid && b.UnpairedAt == null);
        bindings.Should().HaveCount(1);
    }
}
