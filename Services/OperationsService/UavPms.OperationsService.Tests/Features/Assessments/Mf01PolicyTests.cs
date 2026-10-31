using FluentAssertions;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.Assessments.Policies;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Assessments;

/// <summary>Pure MF01 policies (BR-18 – BR-23) and the inspection route check.</summary>
public class Mf01PolicyTests
{
    [Fact]
    public void Personnel_ScopeComesFromCandidateRows_NotFromTheCallersAdminFlag()
    {
        var regionId = Guid.NewGuid();
        var user = new User { Id = Guid.NewGuid(), Status = "Active", IsEmailVerified = true };
        user.UserRoles.Add(new UserRole { Role = new Role { RoleName = "Inspector" } });

        var result = PersonnelEligibilityPolicy.EvaluatePersonnel(
            user, regionId, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2),
            new List<UserGeographicScope>(), new List<ResourceBooking>(), isGlobalAdmin: true);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be("OUTSIDE_MANAGEMENT_SCOPE");
    }

    [Fact]
    public void Personnel_FieldUserWithManagerRole_IsInvalidRole()
    {
        var regionId = Guid.NewGuid();
        var user = new User { Id = Guid.NewGuid(), Status = "Active", IsEmailVerified = true };
        user.UserRoles.Add(new UserRole { Role = new Role { RoleName = "Inspector" } });
        user.UserRoles.Add(new UserRole { Role = new Role { RoleName = "Manager" } });

        var result = PersonnelEligibilityPolicy.EvaluatePersonnel(
            user, regionId, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2),
            new List<UserGeographicScope> { new() { UserId = user.Id, RegionId = regionId } }, new List<ResourceBooking>());

        result.ReasonCode.Should().Be("INVALID_ROLE");
    }

    [Theory]
    [InlineData("MOTORS")]
    [InlineData("FRAME_BODY")]
    [InlineData("FLIGHT_CONTROLLER")]
    [InlineData("GPS")]
    [InlineData("PROPULSION")]
    public void TechnicalHealth_KnownCriticalItemFailing_IsCritical_EvenWithoutFlag(string code)
    {
        var result = DroneTechnicalHealthEvaluationPolicy.Evaluate(
            new List<DroneMetricSubmitDto> { new(code, "S", null, BoolValue: false, Passed: false, Critical: false) }, 90);

        result.Status.Should().Be(DroneTechnicalInspectionStatus.Failed);
        result.Health.Should().Be(TechnicalHealth.Critical);
    }

    [Theory]
    [InlineData(20, DroneTechnicalInspectionStatus.Failed, TechnicalHealth.Critical)]
    [InlineData(40, DroneTechnicalInspectionStatus.Passed, TechnicalHealth.Warning)]
    [InlineData(80, DroneTechnicalInspectionStatus.Passed, TechnicalHealth.Healthy)]
    public void TechnicalHealth_BatteryBands(decimal level, DroneTechnicalInspectionStatus status, TechnicalHealth health)
    {
        var result = DroneTechnicalHealthEvaluationPolicy.Evaluate(
            new List<DroneMetricSubmitDto> { new("BATTERY_HEALTH", "Power", level) }, 0);

        result.Status.Should().Be(status);
        result.Health.Should().Be(health);
    }

    [Fact]
    public void TechnicalHealth_RfLinkBelowMinimum_Fails()
    {
        var result = DroneTechnicalHealthEvaluationPolicy.Evaluate(
            new List<DroneMetricSubmitDto> { new("RF_LINK_TELEMETRY", "Communication", 20, Passed: true) }, 90);

        result.Status.Should().Be(DroneTechnicalInspectionStatus.Failed);
    }

    [Fact]
    public void TechnicalHealth_OptionalItemWithoutReading_DoesNotFail()
    {
        var result = DroneTechnicalHealthEvaluationPolicy.Evaluate(
            new List<DroneMetricSubmitDto>
            {
                new("CAMERA_PAYLOAD", "Payload", null, IsRequired: false, Critical: false),
                new("FRAME_BODY", "Airframe", null, BoolValue: true)
            }, 90);

        result.Health.Should().Be(TechnicalHealth.Healthy);
    }

    [Fact]
    public void Suitability_RangeLimitsEachFlight()
    {
        // 30 km corridor, 3 towers, plenty of endurance: one flight by time, but a 12 km range needs 3 flights.
        UavScopeSuitabilityPolicy.Evaluate(30, 3, 120, 12).Should().Match<UavScopeSuitabilityResult>(r =>
            r.Suitability == UavScopeSuitability.SuitableWithSplit && r.EstimatedFlightsNeeded == 3);
        UavScopeSuitabilityPolicy.Evaluate(30, 3, 120, 40).Suitability.Should().Be(UavScopeSuitability.Suitable);
        UavScopeSuitabilityPolicy.Evaluate(30, 3, 120, 5).Suitability.Should().Be(UavScopeSuitability.Unsuitable);
        UavScopeSuitabilityPolicy.Evaluate(1, 1, 0, 10).Suitability.Should().Be(UavScopeSuitability.Unsuitable);
    }

    [Fact]
    public void Suitability_CorridorFollowsTowerOrder()
    {
        Geometry P(double lat) => new Point(105.85, lat) { SRID = 4326 };
        // 0.1° of latitude ≈ 11.1 km; out and back is twice the straight order.
        var straight = UavScopeSuitabilityPolicy.EstimateCorridorKm(new[] { P(21.0), P(21.1), P(21.2) });
        var zigzag = UavScopeSuitabilityPolicy.EstimateCorridorKm(new[] { P(21.0), P(21.2), P(21.1) });

        straight.Should().BeApproximately(22.24, 0.1);
        zigzag.Should().BeApproximately(33.36, 0.1);
    }

    [Fact]
    public void DroneEligibility_NewerFailedInspection_WinsOverOlderPassed()
    {
        var drone = new Uav { Id = Guid.NewGuid(), OperationalStatus = DroneOperationalStatus.Available, TechnicalHealth = TechnicalHealth.Healthy };
        var start = DateTime.UtcNow.AddHours(1);
        var inspections = new[]
        {
            new DroneTechnicalInspection { DroneId = drone.Id, Status = DroneTechnicalInspectionStatus.Passed, Health = TechnicalHealth.Healthy, CompletedAt = DateTime.UtcNow.AddDays(-2), ValidUntil = DateTime.UtcNow.AddDays(5) },
            new DroneTechnicalInspection { DroneId = drone.Id, Status = DroneTechnicalInspectionStatus.Failed, Health = TechnicalHealth.Critical, CompletedAt = DateTime.UtcNow.AddHours(-1), ValidUntil = DateTime.UtcNow.AddDays(7) },
        };

        var result = DroneEligibilityPolicy.Evaluate(drone, inspections, Array.Empty<ResourceBooking>(), start, start.AddHours(1), 1, 2);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be("TECHNICAL_HEALTH_CRITICAL");
    }

    [Fact]
    public void DroneEligibility_InspectionExpiredBeforePlannedStart_RequiresInspection()
    {
        var drone = new Uav { Id = Guid.NewGuid(), OperationalStatus = DroneOperationalStatus.Available, TechnicalHealth = TechnicalHealth.Healthy };
        var start = DateTime.UtcNow.AddDays(3);
        var inspections = new[]
        {
            new DroneTechnicalInspection { DroneId = drone.Id, Status = DroneTechnicalInspectionStatus.Passed, Health = TechnicalHealth.Healthy, CompletedAt = DateTime.UtcNow.AddDays(-6), ValidUntil = DateTime.UtcNow.AddDays(1) },
        };

        var result = DroneEligibilityPolicy.Evaluate(drone, inspections, Array.Empty<ResourceBooking>(), start, start.AddHours(1), 1, 2);

        result.IsEligible.Should().BeFalse();
        result.TechnicalEligibility.Should().Be(ResourceEligibilityStatus.RequiresInspection);
        result.ReasonCode.Should().Be("NO_VALID_TECHNICAL_INSPECTION");
    }

    [Fact]
    public void SiteFeasibility_TowerWithoutCoordinates_FailsWithoutBoundary()
    {
        var region = new Region { Id = Guid.NewGuid() };
        var tower = new Tower { Id = Guid.NewGuid(), RegionId = region.Id };

        var result = SiteFeasibilityPolicy.Evaluate(region, new[] { tower }, null, new[] { tower.Id });

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("TOWER_MISSING_GEOLOCATION");
    }

    [Fact]
    public async Task InspectionRoute_BodyDroneIdDifferentFromRoute_IsRejected()
    {
        var service = new Mock<IDroneTechnicalInspectionService>(MockBehavior.Strict);
        var controller = new DroneTechnicalInspectionController(service.Object);

        var act = () => controller.SubmitUavInspection(Guid.NewGuid(), new DroneInspectionSubmitRequest(Guid.NewGuid(), null), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("DRONE_ID_MISMATCH*");
    }
}
