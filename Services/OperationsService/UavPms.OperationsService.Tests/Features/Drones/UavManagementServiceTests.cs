using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.Drones.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Drones;

public class UavManagementServiceTests
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
            .UseInMemoryDatabase(databaseName: $"UAV_TestDb_{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options, currentUser);
    }

    [Fact]
    public async Task CreateDroneAsync_ValidRequest_CreatesUavSuccessfully()
    {
        var managerId = Guid.NewGuid();
        var userMock = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(userMock.Object);
        var inspectionMock = new Mock<IDroneTechnicalInspectionService>();
        var service = new UavManagementService(db, userMock.Object, inspectionMock.Object);

        var request = new CreateUavRequest
        {
            Code = "UAV-101",
            Name = "Survey Hexacopter 01",
            Model = "DJI Matrice 350 RTK",
            Category = "Hexacopter",
            SerialNumber = "SN-M350-9921",
            FlightEnduranceMinutes = 45.0,
            OperationalRangeKm = 20.0,
            CameraPayloadInfo = "Zenmuse H20N Starlight Night Vision",
            BatteryLevel = 98.0
        };

        var result = await service.CreateDroneAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        result.Code.Should().Be("UAV-101");
        result.Name.Should().Be("Survey Hexacopter 01");
        result.FlightEnduranceMinutes.Should().Be(45.0);
        result.OperationalRangeKm.Should().Be(20.0);
        result.OperationalStatus.Should().Be("AVAILABLE");
        result.TechnicalHealth.Should().Be("UNKNOWN");

        var dbDrone = await db.Uavs.SingleOrDefaultAsync(x => x.UavCode == "UAV-101");
        dbDrone.Should().NotBeNull();
        dbDrone!.Name.Should().Be("Survey Hexacopter 01");
    }

    [Fact]
    public async Task CreateDroneAsync_DuplicateCode_ThrowsBusinessRuleException()
    {
        var managerId = Guid.NewGuid();
        var userMock = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(userMock.Object);
        var inspectionMock = new Mock<IDroneTechnicalInspectionService>();
        var service = new UavManagementService(db, userMock.Object, inspectionMock.Object);

        db.Uavs.Add(new Uav { Id = Guid.NewGuid(), UavCode = "UAV-001", Name = "Drone 1", Model = "M300" });
        await db.SaveChangesAsync();

        var request = new CreateUavRequest
        {
            Code = "uav-001",
            Name = "Duplicate Drone",
            Model = "M300"
        };

        var act = async () => await service.CreateDroneAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*đã tồn tại*");
    }

    [Fact]
    public async Task UpdateDroneAsync_UpdatesFieldsAndIncrementsVersion()
    {
        var managerId = Guid.NewGuid();
        var userMock = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(userMock.Object);
        var inspectionMock = new Mock<IDroneTechnicalInspectionService>();
        var service = new UavManagementService(db, userMock.Object, inspectionMock.Object);

        var droneId = Guid.NewGuid();
        var drone = new Uav
        {
            Id = droneId,
            UavCode = "UAV-002",
            Name = "Original Name",
            Model = "M300",
            OperationalStatus = DroneOperationalStatus.Available,
            TechnicalHealth = TechnicalHealth.Healthy,
            Version = 1
        };
        db.Uavs.Add(drone);
        await db.SaveChangesAsync();

        var updateReq = new UpdateUavRequest
        {
            Name = "Updated Inspection Drone 02",
            OperationalStatus = DroneOperationalStatus.Maintenance,
            TechnicalHealth = TechnicalHealth.Warning,
            FlightEnduranceMinutes = 40.0
        };

        var result = await service.UpdateDroneAsync(droneId, updateReq, CancellationToken.None);

        result.Name.Should().Be("Updated Inspection Drone 02");
        result.OperationalStatus.Should().Be("MAINTENANCE");
        result.TechnicalHealth.Should().Be("WARNING");
        result.FlightEnduranceMinutes.Should().Be(40.0);

        var refreshed = await db.Uavs.FindAsync(droneId);
        refreshed!.Version.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task DeleteDroneAsync_SoftDeletesAndDeactivatesFlightController()
    {
        var managerId = Guid.NewGuid();
        var userMock = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(userMock.Object);
        var inspectionMock = new Mock<IDroneTechnicalInspectionService>();
        var service = new UavManagementService(db, userMock.Object, inspectionMock.Object);

        var droneId = Guid.NewGuid();
        var drone = new Uav { Id = droneId, UavCode = "UAV-003", Name = "Drone 3", Model = "M300" };
        var fc = new DroneFlightController
        {
            Id = Guid.NewGuid(),
            DroneId = droneId,
            FlightControllerUid = "FC-UID-333",
            UnpairedAt = null
        };
        drone.FlightControllers.Add(fc);
        db.Uavs.Add(drone);
        await db.SaveChangesAsync();

        var deleted = await service.DeleteDroneAsync(droneId, CancellationToken.None);
        deleted.Should().BeTrue();

        var refreshedDrone = await db.Uavs.FindAsync(droneId);
        refreshedDrone!.IsDeleted.Should().BeTrue();

        var refreshedFc = await db.DroneFlightControllers.FindAsync(fc.Id);
        refreshedFc!.UnpairedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDronesAsync_FilterBySearchAndStatus_ReturnsFilteredResults()
    {
        var userId = Guid.NewGuid();
        var userMock = CreateUserMock(userId, UserRoles.Inspector);
        await using var db = CreateContext(userMock.Object);
        var inspectionMock = new Mock<IDroneTechnicalInspectionService>();
        var service = new UavManagementService(db, userMock.Object, inspectionMock.Object);

        db.Uavs.AddRange(
            new Uav { Id = Guid.NewGuid(), UavCode = "UAV-101", Name = "Hexa Alpha", Model = "M300", OperationalStatus = DroneOperationalStatus.Available, TechnicalHealth = TechnicalHealth.Healthy },
            new Uav { Id = Guid.NewGuid(), UavCode = "UAV-102", Name = "Quad Beta", Model = "Air3", OperationalStatus = DroneOperationalStatus.Maintenance, TechnicalHealth = TechnicalHealth.Critical },
            new Uav { Id = Guid.NewGuid(), UavCode = "UAV-103", Name = "Hexa Gamma", Model = "M350", OperationalStatus = DroneOperationalStatus.Available, TechnicalHealth = TechnicalHealth.Warning }
        );
        await db.SaveChangesAsync();

        var results = await service.GetDronesAsync(search: "Hexa", availableOnly: true, ct: CancellationToken.None);
        results.Should().HaveCount(2);
        results.Select(x => x.Code).Should().Contain(new[] { "UAV-101", "UAV-103" });
    }

    [Fact]
    public async Task EstimateGisScopeDronesAsync_SingleFlightRange_ClassifiesSuitable()
    {
        var userId = Guid.NewGuid();
        var userMock = CreateUserMock(userId, UserRoles.Manager);
        await using var db = CreateContext(userMock.Object);
        var inspectionMock = new Mock<IDroneTechnicalInspectionService>();
        var service = new UavManagementService(db, userMock.Object, inspectionMock.Object);

        db.Uavs.Add(new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = "UAV-001",
            Name = "Long Endurance Drone",
            FlightEnduranceMinutes = 45.0,
            OperationalRangeKm = 20.0,
            OperationalStatus = DroneOperationalStatus.Available,
            TechnicalHealth = TechnicalHealth.Healthy
        });
        await db.SaveChangesAsync();

        // 3km corridor, 5 towers -> ~16 mins total mission -> fits in 1 flight of 45 mins (36 mins effective)
        var request = new GisDroneEstimationRequest
        {
            CorridorLengthKm = 3.0,
            AssetCount = 5
        };

        var result = await service.EstimateGisScopeDronesAsync(request, CancellationToken.None);

        result.Drones.Should().HaveCount(1);
        result.Drones[0].Suitability.Should().Be("SUITABLE");
        result.Drones[0].EstimatedFlightsNeeded.Should().Be(1);
        result.Drones[0].Recommendation.Should().Contain("trọn vẹn");
    }

    [Fact]
    public async Task EstimateGisScopeDronesAsync_LongScope_ClassifiesSuitableWithSplit()
    {
        var userId = Guid.NewGuid();
        var userMock = CreateUserMock(userId, UserRoles.Manager);
        await using var db = CreateContext(userMock.Object);
        var inspectionMock = new Mock<IDroneTechnicalInspectionService>();
        var service = new UavManagementService(db, userMock.Object, inspectionMock.Object);

        db.Uavs.Add(new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = "UAV-002",
            Name = "Standard Drone",
            FlightEnduranceMinutes = 30.0,
            OperationalRangeKm = 10.0,
            OperationalStatus = DroneOperationalStatus.Available,
            TechnicalHealth = TechnicalHealth.Healthy
        });
        await db.SaveChangesAsync();

        // 12km corridor, 25 towers -> ~95 mins mission -> requires 4 flights
        var request = new GisDroneEstimationRequest
        {
            CorridorLengthKm = 12.0,
            AssetCount = 25
        };

        var result = await service.EstimateGisScopeDronesAsync(request, CancellationToken.None);

        result.Drones[0].Suitability.Should().Be("SUITABLE_WITH_SPLIT");
        result.Drones[0].EstimatedFlightsNeeded.Should().BeGreaterThan(1);
        result.Drones[0].Recommendation.Should().Contain("chia thành");
    }

    [Fact]
    public async Task EstimateGisScopeDronesAsync_CriticalTechnicalHealth_ClassifiesUnsuitable()
    {
        var userId = Guid.NewGuid();
        var userMock = CreateUserMock(userId, UserRoles.Manager);
        await using var db = CreateContext(userMock.Object);
        var inspectionMock = new Mock<IDroneTechnicalInspectionService>();
        var service = new UavManagementService(db, userMock.Object, inspectionMock.Object);

        db.Uavs.Add(new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = "UAV-004",
            Name = "Damaged Drone",
            FlightEnduranceMinutes = 30.0,
            OperationalRangeKm = 10.0,
            OperationalStatus = DroneOperationalStatus.Available,
            TechnicalHealth = TechnicalHealth.Critical
        });
        await db.SaveChangesAsync();

        var request = new GisDroneEstimationRequest { CorridorLengthKm = 2.0, AssetCount = 4 };
        var result = await service.EstimateGisScopeDronesAsync(request, CancellationToken.None);

        result.Drones[0].Suitability.Should().Be("UNSUITABLE");
        result.Drones[0].Recommendation.Should().Contain("Critical");
    }

    [Fact]
    public async Task SubmitBasicTechnicalCheckAsync_InvokesInspectionServiceWithChecklist()
    {
        var techId = Guid.NewGuid();
        var userMock = CreateUserMock(techId, UserRoles.MaintenanceTechnician);
        await using var db = CreateContext(userMock.Object);

        var droneId = Guid.NewGuid();
        db.Uavs.Add(new Uav
        {
            Id = droneId,
            UavCode = "UAV-005",
            Name = "Inspection Drone 05",
            BatteryLevel = 90.0,
            OperationalStatus = DroneOperationalStatus.Available
        });
        await db.SaveChangesAsync();

        var inspectionMock = new Mock<IDroneTechnicalInspectionService>();
        inspectionMock.Setup(x => x.SubmitInspectionAsync(It.IsAny<DroneInspectionSubmitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DroneTechnicalInspection
            {
                Id = Guid.NewGuid(),
                DroneId = droneId,
                Status = DroneTechnicalInspectionStatus.Passed,
                Health = TechnicalHealth.Healthy,
                CompletedAt = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddDays(7)
            });

        var service = new UavManagementService(db, userMock.Object, inspectionMock.Object);

        var checkRequest = new BasicTechnicalCheckRequest
        {
            FrameBodyPassed = true,
            PropellersPassed = true,
            MotorsPassed = true,
            CameraPayloadPassed = true,
            BatteryPassed = true,
            BatteryPercent = 90,
            BatteryVoltage = 24.5m,
            GpsPassed = true,
            GpsSatelliteCount = 20,
            CommunicationPassed = true,
            FlightControllerPassed = true,
            FlightControllerUid = "BF-F405-A1B2C3D4"
        };

        var result = await service.SubmitBasicTechnicalCheckAsync(droneId, checkRequest, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("Passed");
        result.Health.Should().Be("Healthy");

        inspectionMock.Verify(x => x.SubmitInspectionAsync(
            It.Is<DroneInspectionSubmitRequest>(r =>
                r.DroneId == droneId &&
                r.FlightControllerUid == "BF-F405-A1B2C3D4" &&
                r.Metrics!.Count == 8),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
