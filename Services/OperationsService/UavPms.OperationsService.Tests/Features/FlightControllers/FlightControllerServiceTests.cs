using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.FlightControllers.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.FlightControllers;

public class FlightControllerServiceTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId, string role)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        return mock;
    }

    private static ApplicationDbContext CreateContext(ICurrentUserServices currentUser)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"FC_TestDb_{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options, currentUser);
    }

    [Fact]
    public async Task ResolveByUidAsync_UnregisteredUid_ReturnsRegisteredFalse()
    {
        var technicianId = Guid.NewGuid();
        var user = CreateUserMock(technicianId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        var service = new FlightControllerService(db, user.Object);

        var result = await service.ResolveByUidAsync("UNKNOWN-UID-123", CancellationToken.None);

        result.Should().NotBeNull();
        result.Registered.Should().BeFalse();
        result.FlightControllerUid.Should().Be("UNKNOWN-UID-123");
        result.Drone.Should().BeNull();
        result.Binding.Should().BeNull();
    }

    [Fact]
    public async Task ResolveByUidAsync_RegisteredUid_ReturnsRegisteredTrueAndUpdatesLastSeenAt()
    {
        var technicianId = Guid.NewGuid();
        var user = CreateUserMock(technicianId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        var drone = new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = "UAV-001",
            Model = "SpeedyBee F405 Hexacopter",
            OperationalStatus = DroneOperationalStatus.Available,
            TechnicalHealth = TechnicalHealth.Healthy
        };
        db.Uavs.Add(drone);

        var binding = new DroneFlightController
        {
            Id = Guid.NewGuid(),
            DroneId = drone.Id,
            FlightControllerUid = "BF-UID-001A",
            BoardIdentifier = "SPEEDYBEEF405V3",
            FirmwareName = "Betaflight",
            FirmwareVersion = "4.5.1",
            PairedAt = DateTime.UtcNow.AddDays(-10),
            LastSeenAt = DateTime.UtcNow.AddDays(-1),
            UnpairedAt = null
        };
        db.DroneFlightControllers.Add(binding);
        await db.SaveChangesAsync();

        var service = new FlightControllerService(db, user.Object);

        var beforeCall = DateTime.UtcNow;
        var result = await service.ResolveByUidAsync("bf-uid-001a", CancellationToken.None);

        result.Should().NotBeNull();
        result.Registered.Should().BeTrue();
        result.FlightControllerUid.Should().Be("BF-UID-001A");
        result.Drone.Should().NotBeNull();
        result.Drone!.Id.Should().Be(drone.Id);
        result.Drone.Code.Should().Be("UAV-001");
        result.Binding.Should().NotBeNull();
        result.Binding!.BoardIdentifier.Should().Be("SPEEDYBEEF405V3");

        var updatedBinding = await db.DroneFlightControllers.SingleAsync(b => b.Id == binding.Id);
        updatedBinding.LastSeenAt.Should().BeOnOrAfter(beforeCall);
    }

    [Fact]
    public async Task PairFlightControllerAsync_PairsNewFlightController_Success()
    {
        var technicianId = Guid.NewGuid();
        var user = CreateUserMock(technicianId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        var drone = new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = "UAV-002",
            Model = "DJI Matrice 300",
            OperationalStatus = DroneOperationalStatus.Available
        };
        db.Uavs.Add(drone);
        await db.SaveChangesAsync();

        var service = new FlightControllerService(db, user.Object);

        var request = new PairFlightControllerRequest(
            FlightControllerUid: "BF-F722-XYZ999",
            BoardIdentifier: "MATEKF722",
            FirmwareName: "Betaflight",
            FirmwareVersion: "4.5.0"
        );

        var binding = await service.PairFlightControllerAsync(drone.Id, request, CancellationToken.None);

        binding.Should().NotBeNull();
        binding.DroneId.Should().Be(drone.Id);
        binding.FlightControllerUid.Should().Be("BF-F722-XYZ999");
        binding.BoardIdentifier.Should().Be("MATEKF722");
        binding.IsActive.Should().BeTrue();

        var inDb = await db.DroneFlightControllers.SingleAsync(b => b.Id == binding.Id);
        inDb.UnpairedAt.Should().BeNull();
        inDb.FlightControllerUid.Should().Be("BF-F722-XYZ999");
    }

    [Fact]
    public async Task PairFlightControllerAsync_ReplacesExistingBindingOnSameDrone_DeactivatesPrevious( )
    {
        // BR-02 & BR-03
        var technicianId = Guid.NewGuid();
        var user = CreateUserMock(technicianId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        var drone = new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = "UAV-003",
            Model = "Tarot 680 Pro"
        };
        db.Uavs.Add(drone);

        var oldBinding = new DroneFlightController
        {
            Id = Guid.NewGuid(),
            DroneId = drone.Id,
            FlightControllerUid = "OLD-FC-111",
            BoardIdentifier = "F405-OLD",
            PairedAt = DateTime.UtcNow.AddMonths(-1),
            UnpairedAt = null
        };
        db.DroneFlightControllers.Add(oldBinding);
        await db.SaveChangesAsync();

        var service = new FlightControllerService(db, user.Object);

        var newRequest = new PairFlightControllerRequest(
            FlightControllerUid: "NEW-FC-222",
            BoardIdentifier: "F722-NEW",
            FirmwareName: "Betaflight",
            FirmwareVersion: "4.5.1"
        );

        var newBinding = await service.PairFlightControllerAsync(drone.Id, newRequest, CancellationToken.None);

        var oldInDb = await db.DroneFlightControllers.SingleAsync(b => b.Id == oldBinding.Id);
        oldInDb.UnpairedAt.Should().NotBeNull();

        var newInDb = await db.DroneFlightControllers.SingleAsync(b => b.Id == newBinding.Id);
        newInDb.UnpairedAt.Should().BeNull();
        newInDb.FlightControllerUid.Should().Be("NEW-FC-222");
    }

    [Fact]
    public async Task PairFlightControllerAsync_SameUidOnAnotherDrone_DeactivatesOtherDroneBinding()
    {
        // BR-01
        var technicianId = Guid.NewGuid();
        var user = CreateUserMock(technicianId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        var droneA = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-A" };
        var droneB = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-B" };
        db.Uavs.AddRange(droneA, droneB);

        var bindingA = new DroneFlightController
        {
            Id = Guid.NewGuid(),
            DroneId = droneA.Id,
            FlightControllerUid = "SHARED-FC-UID",
            UnpairedAt = null
        };
        db.DroneFlightControllers.Add(bindingA);
        await db.SaveChangesAsync();

        var service = new FlightControllerService(db, user.Object);

        // Move FC from DroneA to DroneB
        var pairToB = new PairFlightControllerRequest(
            FlightControllerUid: "SHARED-FC-UID",
            BoardIdentifier: "F405"
        );

        var bindingB = await service.PairFlightControllerAsync(droneB.Id, pairToB, CancellationToken.None);

        var bindingAInDb = await db.DroneFlightControllers.SingleAsync(b => b.Id == bindingA.Id);
        bindingAInDb.UnpairedAt.Should().NotBeNull();

        var bindingBInDb = await db.DroneFlightControllers.SingleAsync(b => b.Id == bindingB.Id);
        bindingBInDb.UnpairedAt.Should().BeNull();
        bindingBInDb.DroneId.Should().Be(droneB.Id);
    }

    [Fact]
    public async Task UnpairFlightControllerAsync_DeactivatesActiveBinding_Success()
    {
        var technicianId = Guid.NewGuid();
        var user = CreateUserMock(technicianId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);

        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-004" };
        db.Uavs.Add(drone);

        var binding = new DroneFlightController
        {
            Id = Guid.NewGuid(),
            DroneId = drone.Id,
            FlightControllerUid = "FC-TO-UNPAIR",
            UnpairedAt = null
        };
        db.DroneFlightControllers.Add(binding);
        await db.SaveChangesAsync();

        var service = new FlightControllerService(db, user.Object);

        var result = await service.UnpairFlightControllerAsync(drone.Id, CancellationToken.None);
        result.Should().BeTrue();

        var inDb = await db.DroneFlightControllers.SingleAsync(b => b.Id == binding.Id);
        inDb.UnpairedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitInspectionAsync_WithMismatchedFlightController_ThrowsBusinessRuleException()
    {
        // BR-05
        var technicianId = Guid.NewGuid();
        var user = CreateUserMock(technicianId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);
        db.Users.Add(new User { Id = technicianId, Status = "Active" });

        var droneA = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-001", BatteryLevel = 90 };
        var droneB = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-002", BatteryLevel = 90 };
        db.Uavs.AddRange(droneA, droneB);

        // FC-002 is paired with droneB
        var bindingB = new DroneFlightController
        {
            Id = Guid.NewGuid(),
            DroneId = droneB.Id,
            FlightControllerUid = "FC-FOR-DRONE-B",
            UnpairedAt = null
        };
        db.DroneFlightControllers.Add(bindingB);
        await db.SaveChangesAsync();

        var inspectionService = new DroneTechnicalInspectionService(db, user.Object);

        // Technician tries to submit inspection for droneA using FC-FOR-DRONE-B
        var request = new DroneInspectionSubmitRequest(
            DroneId: droneA.Id,
            Notes: "Testing mismatch",
            Metrics: new List<DroneMetricSubmitDto>
            {
                new("BATTERY_VOLTAGE", "power", 16.8m, null, "16.8V", "V")
            },
            FlightControllerUid: "FC-FOR-DRONE-B"
        );

        var act = () => inspectionService.SubmitInspectionAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*Connected Flight Controller belongs to UAV-002, but this inspection requires UAV-001*");
    }

    [Fact]
    public async Task SubmitInspectionAsync_WithUnregisteredFlightController_ThrowsBusinessRuleException()
    {
        var technicianId = Guid.NewGuid();
        var user = CreateUserMock(technicianId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);
        db.Users.Add(new User { Id = technicianId, Status = "Active" });

        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-001", BatteryLevel = 90 };
        db.Uavs.Add(drone);
        await db.SaveChangesAsync();

        var inspectionService = new DroneTechnicalInspectionService(db, user.Object);

        var request = new DroneInspectionSubmitRequest(
            DroneId: drone.Id,
            Notes: "Unregistered FC",
            Metrics: new List<DroneMetricSubmitDto>
            {
                new("BATTERY_VOLTAGE", "power", 16.8m, null, "16.8V", "V")
            },
            FlightControllerUid: "NON-EXISTENT-FC"
        );

        var act = () => inspectionService.SubmitInspectionAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*chưa được đăng ký với bất kỳ UAV nào*");
    }

    [Fact]
    public async Task SubmitInspectionAsync_WithValidPairedFlightController_SucceedsAndUpdatesLastSeen()
    {
        var technicianId = Guid.NewGuid();
        var user = CreateUserMock(technicianId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(user.Object);
        db.Users.Add(new User { Id = technicianId, Status = "Active" });

        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-001", BatteryLevel = 95 };
        db.Uavs.Add(drone);

        var binding = new DroneFlightController
        {
            Id = Guid.NewGuid(),
            DroneId = drone.Id,
            FlightControllerUid = "VALID-FC-001",
            LastSeenAt = DateTime.UtcNow.AddDays(-2),
            UnpairedAt = null
        };
        db.DroneFlightControllers.Add(binding);
        await db.SaveChangesAsync();

        var inspectionService = new DroneTechnicalInspectionService(db, user.Object);

        var request = new DroneInspectionSubmitRequest(
            DroneId: drone.Id,
            Notes: "Valid FC match",
            Metrics: new List<DroneMetricSubmitDto>
            {
                new("BATTERY_VOLTAGE", "power", 16.8m, null, "16.8V", "V")
            },
            FlightControllerUid: "valid-fc-001"
        );

        var beforeCall = DateTime.UtcNow;
        var inspection = await inspectionService.SubmitInspectionAsync(request, CancellationToken.None);

        inspection.Should().NotBeNull();
        inspection.DroneId.Should().Be(drone.Id);

        var bindingInDb = await db.DroneFlightControllers.SingleAsync(b => b.Id == binding.Id);
        bindingInDb.LastSeenAt.Should().BeOnOrAfter(beforeCall);
    }
}
