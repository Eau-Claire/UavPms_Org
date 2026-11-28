using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Enums;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// BE-REQ-101 Gate 5 — MF01 chỉ nhận số lượng nhân sự và chỉ chọn Drone Ready (FR-004..FR-007, HC-01).
/// </summary>
public sealed class Be101Mf01HeadcountGate5Tests : IDisposable
{
    private const string Mf01Url = "/api/v1/pre-mission-assessments";
    private const int MaxValidityDays = 7;

    private readonly Be101World _world = new();

    public void Dispose() => _world.Dispose();

    private async Task<int> CountAssessmentsAsync() =>
        await _world.WithDbAsync(db => db.PreMissionAssessments.CountAsync());

    private async Task<int> CountInspectionsAsync() =>
        await _world.WithDbAsync(db => db.DroneTechnicalInspections.CountAsync());

    private async Task<Guid> SeedManagerAsync(Be101World.RegionSeed region) =>
        await _world.SeedUserAsync(new[] { UserRoles.Manager }, region.RegionId);

    // ---------------------------------------------------------------- FR-004

    [Fact]
    public async Task FR004_Manager_CreatesWithSelectedReadyDrones_WithoutRunningBist()
    {
        var region = await _world.SeedRegionAsync();
        var selectedDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var otherReadyDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var managerId = await SeedManagerAsync(region);
        var inspectionsBefore = await CountInspectionsAsync();

        var response = await Be101World.PostJsonAsync(
            _world.Client(managerId, UserRoles.Manager), Mf01Url,
            Be101World.Mf01Payload(region, droneIds: new[] { selectedDrone }));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(response);
        var candidateIds = body.GetProperty("droneCandidates").EnumerateArray()
            .Select(c => c.GetProperty("droneId").GetGuid())
            .ToList();
        candidateIds.Should().Equal(selectedDrone);
        candidateIds.Should().NotContain(otherReadyDrone);
        (await CountInspectionsAsync()).Should().Be(inspectionsBefore);
    }

    [Fact]
    public async Task FR004_ReEvaluate_KeepsSelectedDronesAndDoesNotRunBist()
    {
        var region = await _world.SeedRegionAsync();
        var selectedDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var managerId = await SeedManagerAsync(region);
        var client = _world.Client(managerId, UserRoles.Manager);

        var created = await Be101World.PostJsonAsync(client, Mf01Url, Be101World.Mf01Payload(region, droneIds: new[] { selectedDrone }));
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var assessmentId = (await Be101World.ReadJsonAsync(created)).GetProperty("id").GetGuid();
        var inspectionsBefore = await CountInspectionsAsync();

        var reEvaluated = await Be101World.PostJsonAsync(client, $"{Mf01Url}/{assessmentId}/re-evaluate", null);

        reEvaluated.StatusCode.Should().Be(HttpStatusCode.OK, await reEvaluated.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(reEvaluated);
        body.GetProperty("droneCandidates").EnumerateArray()
            .Select(c => c.GetProperty("droneId").GetGuid())
            .Should().Equal(selectedDrone);
        (await CountInspectionsAsync()).Should().Be(inspectionsBefore);
    }

    // ---------------------------------------------------------------- FR-005

    [Theory]
    [InlineData(UserRoles.Manager)]
    [InlineData(UserRoles.SystemAdmin)]
    public async Task FR005_HeadcountOnly_IsSavedAndReadBack_WithoutAnyUserId(string role)
    {
        var region = await _world.SeedRegionAsync();
        var readyDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var userId = role == UserRoles.SystemAdmin
            ? await _world.SeedUserAsync(new[] { role })
            : await _world.SeedUserAsync(new[] { role }, region.RegionId);
        var client = _world.Client(userId, role);

        var response = await Be101World.PostJsonAsync(
            client, Mf01Url,
            Be101World.Mf01Payload(region, inspectorCount: 2, analystCount: 1, technicianCount: 3, droneIds: new[] { readyDrone }));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var created = await Be101World.ReadJsonAsync(response);
        created.GetProperty("inspectorCount").GetInt32().Should().Be(2);
        created.GetProperty("analystCount").GetInt32().Should().Be(1);
        created.GetProperty("technicianCount").GetInt32().Should().Be(3);

        var assessmentId = created.GetProperty("id").GetGuid();
        var read = await client.GetAsync($"{Mf01Url}/{assessmentId}");
        read.StatusCode.Should().Be(HttpStatusCode.OK, await read.Content.ReadAsStringAsync());
        var stored = await Be101World.ReadJsonAsync(read);
        stored.GetProperty("inspectorCount").GetInt32().Should().Be(2);
        stored.GetProperty("analystCount").GetInt32().Should().Be(1);
        stored.GetProperty("technicianCount").GetInt32().Should().Be(3);
    }

    // ---------------------------------------------------------------- FR-006

    [Fact]
    public async Task FR006_ValidUntil_IsCappedAtSevenDays_EvenWhenSettingIsLonger()
    {
        // 500 giờ (~20,8 ngày) trong SystemSettings không được vượt trần 7 ngày của MF01.
        await _world.SetSettingAsync(SettingKeys.AssessmentValidityHours, "500");
        var region = await _world.SeedRegionAsync();
        var readyDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var managerId = await SeedManagerAsync(region);

        var response = await Be101World.PostJsonAsync(
            _world.Client(managerId, UserRoles.Manager), Mf01Url,
            Be101World.Mf01Payload(region, droneIds: new[] { readyDrone }));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(response);
        var validUntil = body.GetProperty("validUntil").GetDateTime().ToUniversalTime();
        validUntil.Should().BeAfter(DateTime.UtcNow);
        validUntil.Should().BeOnOrBefore(DateTime.UtcNow.AddDays(MaxValidityDays).AddMinutes(1));
    }

    // ---------------------------------------------------------------- FR-007

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(-1, 1, 1)]
    [InlineData(1, -2, 1)]
    [InlineData(null, 1, 1)]
    [InlineData(1, null, 1)]
    [InlineData(1, 1, null)]
    public async Task FR007_InvalidOrMissingHeadcount_Returns400InvalidRoleHeadcount_AndSavesNothing(
        int? inspectors, int? analysts, int? technicians)
    {
        var region = await _world.SeedRegionAsync();
        var readyDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var managerId = await SeedManagerAsync(region);

        var response = await Be101World.PostJsonAsync(
            _world.Client(managerId, UserRoles.Manager), Mf01Url,
            Be101World.Mf01Payload(region, inspectors, analysts, technicians, new[] { readyDrone }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(response)).GetProperty("errorCode").GetString().Should().Be("INVALID_ROLE_HEADCOUNT");
        (await CountAssessmentsAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(TechnicalHealth.Warning)]
    [InlineData(TechnicalHealth.Critical)]
    [InlineData(TechnicalHealth.Unknown)]
    public async Task FR007_SelectingDroneThatIsNotReady_Returns409DroneNotReady_AndSavesNothing(TechnicalHealth health)
    {
        var region = await _world.SeedRegionAsync();
        var readyDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var notReadyDrone = await _world.SeedDroneAsync(region.RegionId, health);
        var managerId = await SeedManagerAsync(region);

        var response = await Be101World.PostJsonAsync(
            _world.Client(managerId, UserRoles.Manager), Mf01Url,
            Be101World.Mf01Payload(region, droneIds: new[] { readyDrone, notReadyDrone }));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(response)).GetProperty("errorCode").GetString().Should().Be("DRONE_NOT_READY");
        (await CountAssessmentsAsync()).Should().Be(0);
    }

    // ---------------------------------------------------------------- HC-01 (RBAC của MF01)

    [Fact]
    public async Task HC01_SystemAdminWithoutScope_CreatesMf01_ButManagerOutsideScopeIsForbidden()
    {
        var region = await _world.SeedRegionAsync();
        var otherRegion = await _world.SeedRegionAsync();
        var readyDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var adminId = await _world.SeedUserAsync(new[] { UserRoles.SystemAdmin });
        var outsideManagerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, otherRegion.RegionId);
        var payload = Be101World.Mf01Payload(region, droneIds: new[] { readyDrone });

        var outside = await Be101World.PostJsonAsync(_world.Client(outsideManagerId, UserRoles.Manager), Mf01Url, payload);
        outside.StatusCode.Should().Be(HttpStatusCode.Forbidden, await outside.Content.ReadAsStringAsync());

        var admin = await Be101World.PostJsonAsync(_world.Client(adminId, UserRoles.SystemAdmin), Mf01Url, payload);
        admin.StatusCode.Should().Be(HttpStatusCode.OK, await admin.Content.ReadAsStringAsync());
        (await Be101World.ReadJsonAsync(admin)).GetProperty("inspectorCount").GetInt32().Should().Be(1);
    }

    [Theory]
    [InlineData(UserRoles.Inspector)]
    [InlineData(UserRoles.Analyst)]
    [InlineData(UserRoles.Technician)]
    public async Task HC01_FieldRoles_CannotCreateMf01(string role)
    {
        var region = await _world.SeedRegionAsync();
        var readyDrone = await _world.SeedDroneAsync(region.RegionId, TechnicalHealth.Healthy);
        var userId = await _world.SeedUserAsync(new[] { role }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(userId, role), Mf01Url, Be101World.Mf01Payload(region, droneIds: new[] { readyDrone }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await CountAssessmentsAsync()).Should().Be(0);
    }
}
