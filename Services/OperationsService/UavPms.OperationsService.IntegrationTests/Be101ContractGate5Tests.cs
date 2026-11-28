using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// BE-REQ-101 Gate 5 — HC-03: camelCase, UTC "Z", sắp xếp DESC, lỗi dùng ApiResponse, không dữ liệu giả.
/// </summary>
public sealed class Be101ContractGate5Tests : IDisposable
{
    private readonly Be101World _world = new();

    public void Dispose() => _world.Dispose();

    private static void AssertAllKeysAreCamelCase(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                AssertAllKeysAreCamelCase(item);
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return;

        foreach (var property in element.EnumerateObject())
        {
            char.IsLower(property.Name[0]).Should().BeTrue($"khóa '{property.Name}' phải là camelCase");
            AssertAllKeysAreCamelCase(property.Value);
        }
    }

    [Fact]
    public async Task HC03_InspectionResponse_IsCamelCase_WithUtcTimestamps()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(technicianId, UserRoles.Technician),
            $"/api/v1/uavs/{droneId}/inspections",
            Be101World.ManualInspection());

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(response);
        AssertAllKeysAreCamelCase(body);
        body.GetProperty("readiness").GetString().Should().Be("Ready");
        foreach (var field in new[] { "startedAt", "completedAt", "validUntil" })
            body.GetProperty(field).GetString().Should().EndWith("Z", $"{field} phải là UTC");

        var stored = await _world.WithDbAsync(db => db.DroneTechnicalInspections.SingleAsync(i => i.DroneId == droneId));
        stored.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        stored.ValidUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task HC03_ErrorResponse_UsesApiResponseEnvelope_InCamelCase()
    {
        var region = await _world.SeedRegionAsync();
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(managerId, UserRoles.Manager), "/api/v1/pre-mission-assessments",
            Be101World.Mf01Payload(region, inspectorCount: 0));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(response);
        AssertAllKeysAreCamelCase(body);
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("errorCode").GetString().Should().Be("INVALID_ROLE_HEADCOUNT");
        body.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task HC03_ManualInspection_StoresOnlySubmittedReadings_NoFabricatedNotesOrMetrics()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        var technicianId = await _world.SeedUserAsync(new[] { UserRoles.Technician }, region.RegionId);

        var response = await Be101World.PostJsonAsync(
            _world.Client(technicianId, UserRoles.Technician),
            $"/api/v1/uavs/{droneId}/inspections",
            Be101World.ManualInspection(notes: null, batteryPercent: 77m));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var stored = await _world.WithDbAsync(db =>
            db.DroneTechnicalInspections.Include(i => i.Metrics).SingleAsync(i => i.DroneId == droneId));

        // Nhập tay thì nguồn phải là "manual" và không được tự bịa ghi chú "tự động BIST".
        stored.SourceType.Should().Be("manual");
        stored.Notes.Should().BeNullOrWhiteSpace();
        stored.Metrics.Select(m => m.MetricCode).Should().BeEquivalentTo(new[] { "BATTERY", "GPS" });
        stored.Metrics.Single(m => m.MetricCode == "BATTERY").NumericValue.Should().Be(77m);
    }

    [Fact]
    public async Task HC03_Mf01List_IsNewestFirst()
    {
        var region = await _world.SeedRegionAsync();
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, region.RegionId);
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        await _world.WithDbAsync(async db =>
        {
            db.PreMissionAssessments.AddRange(
                new PreMissionAssessment
                {
                    Id = older, ManagerId = managerId, RegionId = region.RegionId, Status = PreMissionAssessmentStatus.Ready,
                    PlannedStart = DateTime.UtcNow.AddDays(2), PlannedEnd = DateTime.UtcNow.AddDays(2).AddHours(3),
                    ValidUntil = DateTime.UtcNow.AddDays(5), CreatedAt = DateTime.UtcNow.AddDays(-3)
                },
                new PreMissionAssessment
                {
                    Id = newer, ManagerId = managerId, RegionId = region.RegionId, Status = PreMissionAssessmentStatus.Ready,
                    PlannedStart = DateTime.UtcNow.AddDays(2), PlannedEnd = DateTime.UtcNow.AddDays(2).AddHours(3),
                    ValidUntil = DateTime.UtcNow.AddDays(5), CreatedAt = DateTime.UtcNow.AddDays(-1)
                });
            await db.SaveChangesAsync();
        });

        var response = await _world.Client(managerId, UserRoles.Manager).GetAsync("/api/v1/pre-mission-assessments");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var ids = (await Be101World.ReadJsonAsync(response)).EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ToList();
        ids.Should().Equal(newer, older);
    }

    [Fact]
    public async Task HC03_FlightControllerHistory_IsNewestFirst_ForManagerRead()
    {
        var region = await _world.SeedRegionAsync();
        var droneId = await _world.SeedDroneAsync(region.RegionId);
        await _world.SeedFlightControllerBindingAsync(droneId, "FC-OLD", DateTime.UtcNow.AddDays(-10), active: false);
        await _world.SeedFlightControllerBindingAsync(droneId, "FC-NEW", DateTime.UtcNow.AddDays(-1), active: true);
        var managerId = await _world.SeedUserAsync(new[] { UserRoles.Manager }, region.RegionId);

        var response = await _world.Client(managerId, UserRoles.Manager).GetAsync($"/api/v1/drones/{droneId}/flight-controller/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await Be101World.ReadJsonAsync(response);
        AssertAllKeysAreCamelCase(body);
        body.EnumerateArray().Select(b => b.GetProperty("flightControllerUid").GetString())
            .Should().Equal("FC-NEW", "FC-OLD");
    }
}
