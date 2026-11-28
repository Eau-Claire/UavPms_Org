using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// BE-REQ-101 Fix vòng 1 (Codex Auditor): Drone chỉ dùng cho hồ sơ cùng vùng (kể cả SystemAdmin),
/// khóa Findings chuẩn camelCase và route basic-technical-check không sinh số liệu giả.
/// </summary>
public sealed class Be101AuditFixTests : IDisposable
{
    private const string Mf01Url = "/api/v1/pre-mission-assessments";
    private const string RegionMismatchCode = "DRONE_REGION_MISMATCH";

    private readonly Be101World _world = new();

    public void Dispose() => _world.Dispose();

    private async Task<Guid> SeedMf01CreatorAsync(string role, Be101World.RegionSeed region) =>
        role == UserRoles.SystemAdmin
            ? await _world.SeedUserAsync(new[] { role })
            : await _world.SeedUserAsync(new[] { role }, region.RegionId);

    // ------------------------------------------------ Lỗi 1: Drone phải cùng vùng với hồ sơ MF01

    [Theory]
    [InlineData(UserRoles.Manager)]
    [InlineData(UserRoles.SystemAdmin)]
    public async Task Create_ReadyDroneOfAnotherRegion_Returns409DroneRegionMismatch_ForEveryRole(string role)
    {
        var regionA = await _world.SeedRegionAsync();
        var regionB = await _world.SeedRegionAsync();
        var droneOfB = await _world.SeedDroneAsync(regionB.RegionId, TechnicalHealth.Healthy);
        var creatorId = await SeedMf01CreatorAsync(role, regionA);

        var response = await Be101World.PostJsonAsync(
            _world.Client(creatorId, role), Mf01Url, Be101World.Mf01Payload(regionA, droneIds: new[] { droneOfB }));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(response)).GetProperty("errorCode").GetString().Should().Be(RegionMismatchCode);
        (await _world.WithDbAsync(db => db.PreMissionAssessments.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Create_ReadyDroneWithoutAssignedRegion_Returns409DroneRegionMismatch()
    {
        var region = await _world.SeedRegionAsync();
        var unassignedDrone = await _world.SeedDroneAsync(null, TechnicalHealth.Healthy);
        var adminId = await _world.SeedUserAsync(new[] { UserRoles.SystemAdmin });

        var response = await Be101World.PostJsonAsync(
            _world.Client(adminId, UserRoles.SystemAdmin), Mf01Url, Be101World.Mf01Payload(region, droneIds: new[] { unassignedDrone }));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(response)).GetProperty("errorCode").GetString().Should().Be(RegionMismatchCode);
    }

    [Fact]
    public async Task Create_SystemAdminSelectsReadyDroneOfSameRegion_Returns200()
    {
        var region = await _world.SeedRegionAsync();
        var droneOfRegion = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var adminId = await _world.SeedUserAsync(new[] { UserRoles.SystemAdmin });

        var response = await Be101World.PostJsonAsync(
            _world.Client(adminId, UserRoles.SystemAdmin), Mf01Url, Be101World.Mf01Payload(region, droneIds: new[] { droneOfRegion }));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(UserRoles.Manager)]
    [InlineData(UserRoles.SystemAdmin)]
    public async Task ReEvaluate_SwitchingToReadyDroneOfAnotherRegion_Returns409DroneRegionMismatch(string role)
    {
        var regionA = await _world.SeedRegionAsync();
        var regionB = await _world.SeedRegionAsync();
        var droneOfA = await _world.SeedDroneAsync(regionA.RegionId, TechnicalHealth.Healthy);
        var droneOfB = await _world.SeedDroneAsync(regionB.RegionId, TechnicalHealth.Healthy);
        var creatorId = await SeedMf01CreatorAsync(role, regionA);
        var client = _world.Client(creatorId, role);

        var created = await Be101World.PostJsonAsync(client, Mf01Url, Be101World.Mf01Payload(regionA, droneIds: new[] { droneOfA }));
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var assessmentId = (await Be101World.ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var response = await Be101World.PostJsonAsync(
            client, $"{Mf01Url}/{assessmentId}/re-evaluate", new { droneIds = new[] { droneOfB } });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(response)).GetProperty("errorCode").GetString().Should().Be(RegionMismatchCode);
    }

    [Fact]
    public async Task Create_WithoutDroneIds_AutoCandidatesOnlyContainDronesOfTheAssessmentRegion()
    {
        var regionA = await _world.SeedRegionAsync();
        var regionB = await _world.SeedRegionAsync();
        var droneOfA = await _world.SeedDroneAsync(regionA.RegionId, TechnicalHealth.Healthy);
        await _world.SeedDroneAsync(regionB.RegionId, TechnicalHealth.Healthy);
        await _world.SeedDroneAsync(null, TechnicalHealth.Healthy);
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, regionA.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(managerId, UserRoles.Manager), Mf01Url, Be101World.Mf01Payload(regionA));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(response)).GetProperty("droneCandidates").EnumerateArray()
            .Select(c => c.GetProperty("droneId").GetGuid())
            .Should().Equal(droneOfA);
    }

    // ------------------------------------------------ Lỗi 2: khóa Findings chuẩn

    private static readonly string[] LegacyFindingsKeys = { "requiredInspectors", "requiredAnalysts", "requiredTechnicians" };

    private async Task<JsonElement> ReadStoredFindingsAsync(Guid assessmentId)
    {
        var findings = await _world.WithDbAsync(async db =>
            (await db.PreMissionAssessments.SingleAsync(a => a.Id == assessmentId)).Findings);
        using var doc = JsonDocument.Parse(findings);
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task Create_StoresHeadcountInFindingsWithStandardKeys_NotLegacyKeys()
    {
        var region = await _world.SeedRegionAsync();
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, region.RegionId);
        var client = _world.Client(managerId, UserRoles.Manager);

        var created = await Be101World.PostJsonAsync(
            client, Mf01Url, Be101World.Mf01Payload(region, inspectorCount: 2, analystCount: 1, technicianCount: 3));
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var assessmentId = (await Be101World.ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var stored = await ReadStoredFindingsAsync(assessmentId);
        stored.GetProperty("inspectorCount").GetInt32().Should().Be(2);
        stored.GetProperty("analystCount").GetInt32().Should().Be(1);
        stored.GetProperty("technicianCount").GetInt32().Should().Be(3);
        foreach (var legacyKey in LegacyFindingsKeys)
            stored.TryGetProperty(legacyKey, out _).Should().BeFalse($"khóa cũ '{legacyKey}' không được ghi nữa");

        var reEvaluated = await Be101World.PostJsonAsync(
            client, $"{Mf01Url}/{assessmentId}/re-evaluate", new { inspectorCount = 3, analystCount = 2, technicianCount = 1 });
        reEvaluated.StatusCode.Should().Be(HttpStatusCode.OK, await reEvaluated.Content.ReadAsStringAsync());

        var afterReEvaluate = await ReadStoredFindingsAsync(assessmentId);
        afterReEvaluate.GetProperty("inspectorCount").GetInt32().Should().Be(3);
        afterReEvaluate.GetProperty("analystCount").GetInt32().Should().Be(2);
        afterReEvaluate.GetProperty("technicianCount").GetInt32().Should().Be(1);
        foreach (var legacyKey in LegacyFindingsKeys)
            afterReEvaluate.TryGetProperty(legacyKey, out _).Should().BeFalse($"khóa cũ '{legacyKey}' không được ghi nữa");
    }

    [Fact]
    public async Task Get_LegacyFindingsKeys_AreStillReadBack_ForOldRecords()
    {
        var region = await _world.SeedRegionAsync();
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, region.RegionId);
        var assessmentId = Guid.NewGuid();
        await _world.WithDbAsync(async db =>
        {
            db.PreMissionAssessments.Add(new PreMissionAssessment
            {
                Id = assessmentId,
                ManagerId = managerId,
                RegionId = region.RegionId,
                Status = PreMissionAssessmentStatus.NotReady,
                PlannedStart = DateTime.UtcNow.AddDays(2),
                PlannedEnd = DateTime.UtcNow.AddDays(2).AddHours(3),
                ValidUntil = DateTime.UtcNow.AddDays(5),
                Findings = "{\"requiredInspectors\":2,\"requiredAnalysts\":1,\"requiredTechnicians\":3}"
            });
            await db.SaveChangesAsync();
        });

        var response = await _world.Client(managerId, UserRoles.Manager).GetAsync($"{Mf01Url}/{assessmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(response);
        body.GetProperty("inspectorCount").GetInt32().Should().Be(2);
        body.GetProperty("analystCount").GetInt32().Should().Be(1);
        body.GetProperty("technicianCount").GetInt32().Should().Be(3);
    }

    // ------------------------------------------------ Lỗi 3: basic-technical-check không sinh dữ liệu giả

    private static readonly string[] FabricatedFragments =
        { "24.8", "3D_FIX_RTK", "STM32F405", "Betaflight", "RSSI -62", "BF-FC", "N/A", "Cell: 6" };

    [Fact]
    public async Task BasicTechnicalCheck_WithoutAnyReading_Returns400AndSavesNothing()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(technicianId, UserRoles.Technician), $"/api/v1/drones/{droneId}/basic-technical-check", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(response)).GetProperty("errorCode").GetString().Should().Be("METRICS_REQUIRED");
        (await _world.WithDbAsync(db => db.DroneTechnicalInspections.CountAsync(i => i.DroneId == droneId))).Should().Be(0);
    }

    [Fact]
    public async Task BasicTechnicalCheck_PartialInput_StoresOnlyTheReadingsThatWereSent()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(technicianId, UserRoles.Technician),
            $"/api/v1/drones/{droneId}/basic-technical-check",
            new { batteryPercent = 80, gpsSatelliteCount = 14 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var inspection = await _world.WithDbAsync(db =>
            db.DroneTechnicalInspections.Include(i => i.Metrics).SingleAsync(i => i.DroneId == droneId));

        inspection.Metrics.Select(m => m.MetricCode).Should().BeEquivalentTo(new[] { "BATTERY_HEALTH", "GPS_RTK_FIX" });
        inspection.Notes.Should().BeNullOrWhiteSpace();

        var storedText = string.Join(" | ", inspection.Metrics.SelectMany(m => new[] { m.ValueText, m.Metadata }).Append(inspection.Notes));
        foreach (var fabricated in FabricatedFragments)
            storedText.Should().NotContain(fabricated, "route basic-technical-check không được tự sinh số liệu");
    }
}
