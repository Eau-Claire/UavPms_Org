using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Enums;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// BE-REQ-101 Gate 5 — Drone Detail BIST / FC / nhập tay (FR-001, FR-002, FR-003, HC-01, HC-02).
/// Chạy qua HTTP + JWT thật, chỉ DB là InMemory (xem Be101ApiFactory).
/// </summary>
public sealed class Be101DroneBistGate5Tests : IDisposable
{
    private readonly Be101World _world = new();

    public void Dispose() => _world.Dispose();

    private static string InspectionUrl(Guid droneId) => $"/api/v1/uavs/{droneId}/inspections";

    private async Task<Guid> SeedUserForAsync(string role, Guid regionId) =>
        role == UserRoles.SystemAdmin
            ? await _world.SeedUserAsync(new[] { role })
            : await _world.SeedUserAsync(new[] { role }, regionId);

    private async Task<int> CountInspectionsAsync(Guid droneId) =>
        await _world.WithDbAsync(db => db.DroneTechnicalInspections.CountAsync(i => i.DroneId == droneId));

    // ---------------------------------------------------------------- FR-001

    [Fact]
    public async Task FR001_TechnicianInScope_ManualPassed_SavesInspectionAndMarksDroneReady()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(technicianId, UserRoles.Technician), InspectionUrl(droneId), Be101World.ManualInspection());

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(response);
        body.GetProperty("status").GetString().Should().Be("Passed");
        body.GetProperty("sourceType").GetString().Should().Be("manual");
        body.GetProperty("readiness").GetString().Should().Be("Ready");

        var inspection = await _world.WithDbAsync(db =>
            db.DroneTechnicalInspections.Include(i => i.Metrics).SingleAsync(i => i.DroneId == droneId));
        inspection.Notes.Should().Be("Đo tại hiện trường");
        inspection.SourceType.Should().Be("manual");
        inspection.Metrics.Should().HaveCount(2);

        var health = await _world.WithDbAsync(async db => (await db.Uavs.SingleAsync(u => u.Id == droneId)).TechnicalHealth);
        health.Should().Be(TechnicalHealth.Healthy);
    }

    [Fact]
    public async Task FR001_TechnicianInScope_FailedConclusionWithFailedBattery_MarksMaintenanceRequired()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(technicianId, UserRoles.Technician),
            InspectionUrl(droneId),
            Be101World.ManualInspection(conclusion: "Failed", batteryPercent: 5m, batteryPassed: false));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(response);
        body.GetProperty("status").GetString().Should().Be("Failed");
        body.GetProperty("readiness").GetString().Should().Be("MaintenanceRequired");

        var health = await _world.WithDbAsync(async db => (await db.Uavs.SingleAsync(u => u.Id == droneId)).TechnicalHealth);
        health.Should().Be(TechnicalHealth.Critical);
    }

    [Fact]
    public async Task FR001_FailedConclusion_WinsOverPassingReadings()
    {
        // Kết luận của Technician là quyết định cuối: số đo đẹp nhưng kết luận Failed vẫn là MaintenanceRequired.
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(technicianId, UserRoles.Technician),
            InspectionUrl(droneId),
            Be101World.ManualInspection(conclusion: "Failed"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(response);
        body.GetProperty("status").GetString().Should().Be("Failed");
        body.GetProperty("readiness").GetString().Should().Be("MaintenanceRequired");

        var health = await _world.WithDbAsync(async db => (await db.Uavs.SingleAsync(u => u.Id == droneId)).TechnicalHealth);
        health.Should().Be(TechnicalHealth.Critical);
    }

    [Fact]
    public async Task FR001_FlightControllerSource_KeepsSourceTypeFc()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        await _world.SeedFlightControllerBindingAsync(droneId, "BF-F405-GATE5", DateTime.UtcNow.AddDays(-3), active: true);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(technicianId, UserRoles.Technician),
            InspectionUrl(droneId),
            Be101World.ManualInspection(sourceType: "fc", flightControllerUid: "bf-f405-gate5"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(response);
        body.GetProperty("sourceType").GetString().Should().Be("fc");
        body.GetProperty("readiness").GetString().Should().Be("Ready");
    }

    [Fact]
    public async Task FR001_LegacyInspectionRoute_IgnoresPreMissionAssessmentId()
    {
        // BIST không còn gắn vào MF01: preMissionAssessmentId lạ không được làm hỏng (404) việc ghi kiểm tra.
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, region.RegionId);

        var body = new Dictionary<string, object?>
        {
            ["droneId"] = droneId,
            ["preMissionAssessmentId"] = Guid.NewGuid(),
            ["sourceType"] = "manual",
            ["conclusion"] = "Passed",
            ["notes"] = "Ghi qua route cũ",
            ["metrics"] = Be101World.ManualMetrics()
        };

        var response = await Be101World.PostJsonAsync(
            _world.Client(technicianId, UserRoles.Technician), "/api/v1/drone-technical-inspections", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await CountInspectionsAsync(droneId)).Should().Be(1);
    }

    // ---------------------------------------------------------------- FR-002

    [Theory]
    [InlineData(UserRoles.SystemAdmin)]
    [InlineData(UserRoles.Technician)]
    [InlineData(UserRoles.Manager)]
    [InlineData(UserRoles.Inspector)]
    public async Task FR002_AllowedRoles_ReadLatestInspectionAndHistoryNewestFirst(string role)
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        await _world.SeedInspectionAsync(droneId, DroneTechnicalInspectionStatus.Failed, TechnicalHealth.Critical, DateTime.UtcNow.AddDays(-5));
        await _world.SeedInspectionAsync(droneId, DroneTechnicalInspectionStatus.Passed, TechnicalHealth.Healthy, DateTime.UtcNow.AddDays(-1));
        var userId = await SeedUserForAsync(role, region.RegionId);
        var client = _world.Client(userId, role);

        var latest = await client.GetAsync($"/api/v1/uavs/{droneId}/inspections/latest");
        latest.StatusCode.Should().Be(HttpStatusCode.OK, await latest.Content.ReadAsStringAsync());
        var latestBody = await Be101World.ReadJsonAsync(latest);
        latestBody.GetProperty("status").GetString().Should().Be("Passed");
        latestBody.GetProperty("readiness").GetString().Should().Be("Ready");

        var history = await client.GetAsync($"/api/v1/drones/{droneId}/technical-inspections");
        history.StatusCode.Should().Be(HttpStatusCode.OK, await history.Content.ReadAsStringAsync());
        var items = (await Be101World.ReadJsonAsync(history)).EnumerateArray().ToList();
        items.Should().HaveCount(2);
        items[0].GetProperty("status").GetString().Should().Be("Passed");
        items[1].GetProperty("status").GetString().Should().Be("Failed");
        items[0].GetProperty("completedAt").GetDateTime().Should().BeAfter(items[1].GetProperty("completedAt").GetDateTime());
    }

    // ---------------------------------------------------------------- FR-003

    private static Task<HttpResponseMessage> CallAsync(HttpClient client, string endpoint, Guid droneId) => endpoint switch
    {
        "latest" => client.GetAsync($"/api/v1/uavs/{droneId}/inspections/latest"),
        "history" => client.GetAsync($"/api/v1/drones/{droneId}/technical-inspections"),
        "flightController" => client.GetAsync($"/api/v1/drones/{droneId}/flight-controller"),
        "flightControllerHistory" => client.GetAsync($"/api/v1/drones/{droneId}/flight-controller/history"),
        "write" => Be101World.PostJsonAsync(client, InspectionUrl(droneId), Be101World.ManualInspection()),
        "legacyWrite" => Be101World.PostJsonAsync(client, "/api/v1/drone-technical-inspections",
            new Dictionary<string, object?>
            {
                ["droneId"] = droneId, ["sourceType"] = "manual", ["conclusion"] = "Passed", ["metrics"] = Be101World.ManualMetrics()
            }),
        "basicCheck" => Be101World.PostJsonAsync(client, $"/api/v1/drones/{droneId}/basic-technical-check", new { }),
        "pairFlightController" => Be101World.PostJsonAsync(client, $"/api/v1/drones/{droneId}/flight-controller",
            new { flightControllerUid = "FC-GATE5-PAIR" }),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "Endpoint Gate 5 không xác định.")
    };

    [Theory]
    [InlineData("latest")]
    [InlineData("history")]
    [InlineData("flightController")]
    [InlineData("flightControllerHistory")]
    [InlineData("write")]
    [InlineData("legacyWrite")]
    [InlineData("basicCheck")]
    [InlineData("pairFlightController")]
    public async Task FR003_AnalystOnly_IsForbiddenOnEveryDroneBistEndpoint_AndNothingIsWritten(string endpoint)
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var analystId = await _world.SeedUserAsync(new[] { UserRoles.Analyst }, region.RegionId);

        var response = await CallAsync(_world.Client(analystId, UserRoles.Analyst), endpoint, droneId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await CountInspectionsAsync(droneId)).Should().Be(0);
        (await _world.WithDbAsync(db => db.DroneFlightControllers.CountAsync(f => f.DroneId == droneId))).Should().Be(0);
    }

    [Theory]
    [InlineData(UserRoles.Manager, "write")]
    [InlineData(UserRoles.Manager, "legacyWrite")]
    [InlineData(UserRoles.Manager, "basicCheck")]
    [InlineData(UserRoles.Manager, "pairFlightController")]
    [InlineData(UserRoles.Inspector, "write")]
    [InlineData(UserRoles.Inspector, "legacyWrite")]
    [InlineData(UserRoles.Inspector, "basicCheck")]
    [InlineData(UserRoles.Inspector, "pairFlightController")]
    public async Task FR003_ManagerAndInspector_AreReadOnly_AndNothingIsWritten(string role, string endpoint)
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var userId = await _world.SeedUserAsync(new[] { role }, region.RegionId);

        var response = await CallAsync(_world.Client(userId, role), endpoint, droneId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await CountInspectionsAsync(droneId)).Should().Be(0);
        (await _world.WithDbAsync(db => db.DroneFlightControllers.CountAsync(f => f.DroneId == droneId))).Should().Be(0);
    }

    [Theory]
    [InlineData(UserRoles.Technician, "write")]
    [InlineData(UserRoles.Technician, "latest")]
    [InlineData(UserRoles.Manager, "latest")]
    [InlineData(UserRoles.Manager, "history")]
    [InlineData(UserRoles.Inspector, "latest")]
    [InlineData(UserRoles.Inspector, "history")]
    public async Task FR003_HC01_OutsideAssignedRegionScope_IsRejected_AndNothingIsWritten(string role, string endpoint)
    {
        var regionOfUser = await _world.SeedRegionAsync();
        var regionOfDrone = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(regionOfDrone.RegionId);
        await _world.SeedInspectionAsync(droneId, DroneTechnicalInspectionStatus.Passed, TechnicalHealth.Healthy, DateTime.UtcNow.AddDays(-1));
        var userId = await _world.SeedUserAsync(new[] { role }, regionOfUser.RegionId);
        var before = await CountInspectionsAsync(droneId);

        var response = await CallAsync(_world.Client(userId, role), endpoint, droneId);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        (await CountInspectionsAsync(droneId)).Should().Be(before);
    }

    // ---------------------------------------------------------------- HC-01 / HC-02

    [Fact]
    public async Task HC01_SystemAdminWithoutAnyScope_CanWriteAndReadAnyDrone()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var adminId = await _world.SeedUserAsync(new[] { UserRoles.SystemAdmin });
        var client = _world.Client(adminId, UserRoles.SystemAdmin);

        var write = await Be101World.PostJsonAsync(client, InspectionUrl(droneId), Be101World.ManualInspection());
        write.StatusCode.Should().Be(HttpStatusCode.OK, await write.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(write)).GetProperty("readiness").GetString().Should().Be("Ready");

        var read = await client.GetAsync($"/api/v1/uavs/{droneId}/inspections/latest");
        read.StatusCode.Should().Be(HttpStatusCode.OK, await read.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HC02_ManagerPlusTechnician_InScope_UnionOfRolesAllowsWrite()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var userId = await _world.SeedUserAsync(new[] { UserRoles.Manager, UserRoles.Technician }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(userId, UserRoles.Manager, UserRoles.Technician), InspectionUrl(droneId), Be101World.ManualInspection());

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(response)).GetProperty("readiness").GetString().Should().Be("Ready");
        (await CountInspectionsAsync(droneId)).Should().Be(1);
    }

    [Fact]
    public async Task HC02_ManagerPlusInspector_WithoutTechnician_StillCannotWrite()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var userId = await _world.SeedUserAsync(new[] { UserRoles.Manager, UserRoles.Inspector }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(userId, UserRoles.Manager, UserRoles.Inspector), InspectionUrl(droneId), Be101World.ManualInspection());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await CountInspectionsAsync(droneId)).Should().Be(0);
    }
}
