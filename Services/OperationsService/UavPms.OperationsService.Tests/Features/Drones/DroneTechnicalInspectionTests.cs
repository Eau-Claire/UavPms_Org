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
}
