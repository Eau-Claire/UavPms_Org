using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Tests.Features.Drones;

public class DroneTechnicalInspectionTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId, string role)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        return mock;
    }

    private static ApplicationDbContext CreateContext(ICurrentUserServices user)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, user);
    }

    [Fact]
    public async Task SubmitInspection_CriticalMetricFailed_SetsCriticalHealthAndFailedStatus()
    {
        var techId = Guid.NewGuid();
        var user = CreateUserMock(techId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = techId, Status = "Active" });
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-01", OperationalStatus = DroneOperationalStatus.Available };
        db.Uavs.Add(drone);
        await db.SaveChangesAsync();

        var service = new DroneTechnicalInspectionService(db, user.Object);

        var request = new DroneInspectionSubmitRequest(
            drone.Id,
            "Propeller blade crack detected",
            new List<DroneMetricSubmitDto>
            {
                new("PROP_INTEGRITY", "Propulsion", null, BoolValue: false, Passed: false, Critical: true),
                new("BATTERY_HEALTH", "Power", 95, Critical: false)
            }
        );

        var result = await service.SubmitInspectionAsync(request, CancellationToken.None);

        result.Status.Should().Be(DroneTechnicalInspectionStatus.Failed);
        result.Health.Should().Be(TechnicalHealth.Critical);

        var updatedDrone = await db.Uavs.FindAsync(drone.Id);
        updatedDrone!.TechnicalHealth.Should().Be(TechnicalHealth.Critical);
        updatedDrone.LastTechnicalInspectionId.Should().Be(result.Id);
    }

    [Fact]
    public async Task SubmitInspection_AllPassed_SetsHealthyAndPassedStatus()
    {
        var techId = Guid.NewGuid();
        var user = CreateUserMock(techId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = techId, Status = "Active" });
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-02", OperationalStatus = DroneOperationalStatus.Available };
        db.Uavs.Add(drone);
        await db.SaveChangesAsync();

        var service = new DroneTechnicalInspectionService(db, user.Object);

        var request = new DroneInspectionSubmitRequest(
            drone.Id,
            "Regular maintenance passed",
            new List<DroneMetricSubmitDto>
            {
                new("PROP_INTEGRITY", "Propulsion", null, BoolValue: true, Passed: true, Critical: true),
                new("BATTERY_HEALTH", "Power", 98, Passed: true, Critical: false)
            }
        );

        var result = await service.SubmitInspectionAsync(request, CancellationToken.None);

        result.Status.Should().Be(DroneTechnicalInspectionStatus.Passed);
        result.Health.Should().Be(TechnicalHealth.Healthy);

        var updatedDrone = await db.Uavs.FindAsync(drone.Id);
        updatedDrone!.TechnicalHealth.Should().Be(TechnicalHealth.Healthy);
        updatedDrone.LastTechnicalInspectionId.Should().Be(result.Id);
    }

    [Fact]
    public async Task GetLatestInspection_ReturnsMostRecentInspection()
    {
        var techId = Guid.NewGuid();
        var user = CreateUserMock(techId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = techId, Status = "Active" });
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-03", OperationalStatus = DroneOperationalStatus.Available };
        db.Uavs.Add(drone);

        var older = new DroneTechnicalInspection
        {
            DroneId = drone.Id,
            CompletedAt = DateTime.UtcNow.AddDays(-5),
            Status = DroneTechnicalInspectionStatus.Passed,
            Health = TechnicalHealth.Healthy
        };
        var newer = new DroneTechnicalInspection
        {
            DroneId = drone.Id,
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            Status = DroneTechnicalInspectionStatus.Passed,
            Health = TechnicalHealth.Healthy
        };
        db.DroneTechnicalInspections.AddRange(older, newer);
        await db.SaveChangesAsync();

        var service = new DroneTechnicalInspectionService(db, user.Object);
        var latest = await service.GetLatestInspectionAsync(drone.Id, CancellationToken.None);

        latest.Should().NotBeNull();
        latest!.Id.Should().Be(newer.Id);
    }

    [Fact]
    public async Task SubmitInspection_EmptyMetrics_ThrowsBusinessRuleException()
    {
        var techId = Guid.NewGuid();
        var user = CreateUserMock(techId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = techId, Status = "Active" });
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-04", OperationalStatus = DroneOperationalStatus.Available };
        db.Uavs.Add(drone);
        await db.SaveChangesAsync();

        var service = new DroneTechnicalInspectionService(db, user.Object);
        var request = new DroneInspectionSubmitRequest(drone.Id, "No metrics", new List<DroneMetricSubmitDto>());

        var act = () => service.SubmitInspectionAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*METRICS_REQUIRED*");
    }

    [Fact]
    public async Task SubmitInspection_DuplicateMetricInSameSubsystem_ThrowsBusinessRuleException()
    {
        var techId = Guid.NewGuid();
        var user = CreateUserMock(techId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = techId, Status = "Active" });
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-05", OperationalStatus = DroneOperationalStatus.Available };
        db.Uavs.Add(drone);
        await db.SaveChangesAsync();

        var service = new DroneTechnicalInspectionService(db, user.Object);
        var request = new DroneInspectionSubmitRequest(
            drone.Id,
            "Duplicate metric",
            new List<DroneMetricSubmitDto>
            {
                new("BATTERY_HEALTH", "Power", 90),
                new("BATTERY_HEALTH", "Power", 85)
            }
        );

        var act = () => service.SubmitInspectionAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*DUPLICATE_METRIC*");
    }

    [Fact]
    public async Task SubmitInspection_LowBatteryLevel_SetsCriticalFailedEvenIfClientSaysPassed()
    {
        var techId = Guid.NewGuid();
        var user = CreateUserMock(techId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = techId, Status = "Active" });
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-06", OperationalStatus = DroneOperationalStatus.Available, BatteryLevel = 15 };
        db.Uavs.Add(drone);
        await db.SaveChangesAsync();

        var service = new DroneTechnicalInspectionService(db, user.Object);
        var request = new DroneInspectionSubmitRequest(
            drone.Id,
            "Low battery",
            new List<DroneMetricSubmitDto>
            {
                new("BATTERY_HEALTH", "Power", 15, Passed: true, Critical: false)
            }
        );

        var result = await service.SubmitInspectionAsync(request, CancellationToken.None);
        result.Status.Should().Be(DroneTechnicalInspectionStatus.Failed);
        result.Health.Should().Be(TechnicalHealth.Critical);
    }

    [Fact]
    public async Task GetLatestInspection_UnauthorizedCallerRole_ThrowsForbiddenException()
    {
        var viewerId = Guid.NewGuid();
        var user = CreateUserMock(viewerId, "Viewer");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = viewerId, Status = "Active" });
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-07", OperationalStatus = DroneOperationalStatus.Available };
        db.Uavs.Add(drone);
        await db.SaveChangesAsync();

        var service = new DroneTechnicalInspectionService(db, user.Object);
        var act = () => service.GetLatestInspectionAsync(drone.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*DRONE_ACCESS_DENIED*");
    }

    [Fact]
    public async Task GetLatestInspection_UserFromDifferentRegion_ThrowsForbiddenException()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var userRegion = Guid.NewGuid();
        var droneRegion = Guid.NewGuid();
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = userRegion });

        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-SCOPE-01", OperationalStatus = DroneOperationalStatus.Available };
        db.Uavs.Add(drone);

        db.Missions.Add(new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-SCOPE-01",
            Title = "Mission in other region",
            RegionId = droneRegion,
            UavId = drone.Id
        });
        await db.SaveChangesAsync();

        var service = new DroneTechnicalInspectionService(db, user.Object);
        var act = () => service.GetLatestInspectionAsync(drone.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*DRONE_ACCESS_DENIED*");
    }

    [Fact]
    public async Task SubmitInspection_IncrementsDroneVersion()
    {
        var techId = Guid.NewGuid();
        var user = CreateUserMock(techId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = techId, Status = "Active" });
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-VER-01", OperationalStatus = DroneOperationalStatus.Available, Version = 1 };
        db.Uavs.Add(drone);
        await db.SaveChangesAsync();

        var service = new DroneTechnicalInspectionService(db, user.Object);
        var request = new DroneInspectionSubmitRequest(
            drone.Id,
            "Version test",
            new List<DroneMetricSubmitDto>
            {
                new("BATTERY_HEALTH", "Power", 90, Passed: true)
            }
        );

        await service.SubmitInspectionAsync(request, CancellationToken.None);

        var updatedDrone = await db.Uavs.FindAsync(drone.Id);
        updatedDrone!.Version.Should().Be(2);
    }
}
