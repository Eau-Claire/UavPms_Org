using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.FieldExecution.Commands.DeliverDroneHandover;
using UavPms.OperationsService.Application.Features.FieldExecution.Commands.FieldCheckIn;
using UavPms.OperationsService.Application.Features.FieldExecution.Commands.ReturnDroneHandover;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Authorization;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.FieldExecution;

public class Phase2FieldExecutionCqrsTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { UavPms.Shared.Contracts.Constants.UserRoles.SystemAdmin });
        return mock;
    }

    private static ApplicationDbContext CreateContext(ICurrentUserServices user)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options, user);
    }

    [Fact]
    public async Task FieldCheckIn_Succeeds_WhenWithin200Meters()
    {
        var pilotId = Guid.NewGuid();
        var userMock = CreateUserMock(pilotId);
        await using var db = CreateContext(userMock.Object);

        // Tower at coordinates (10.8231, 106.6297)
        var tower = new Tower
        {
            Id = Guid.NewGuid(),
            TowerCode = "T-01",
            Geom = new Point(106.6297, 10.8231) { SRID = 4326 }
        };
        db.Towers.Add(tower);

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            Status = MissionStatus.Assigned,
            Assignments = new List<MissionAssignment>
            {
                new()
                {
                    UserId = pilotId,
                    AssignmentRole = "Pilot",
                    Status = MissionAssignmentStatus.Active,
                    ResponseStatus = MissionAssignmentResponse.Accepted
                }
            },
            MissionTargets = new List<MissionTarget>
            {
                new() { TowerId = tower.Id }
            }
        };
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var missionRepo = new MissionRepository(db);
        var accessFilter = new GeographicAccessFilter(db, userMock.Object);
        var towerRepo = new TowerRepository(db, accessFilter);
        var checkInRepo = new GenericRepository<MissionCheckIn>(db);
        var uow = new UnitOfWork(db);

        var handler = new FieldCheckInCommandHandler(
            missionRepo, towerRepo, checkInRepo, userMock.Object, uow);

        // Pilot is located ~50 meters away
        var pilotLat = 10.8234;
        var pilotLon = 106.6299;

        var command = new FieldCheckInCommand(mission.Id, pilotLat, pilotLon);
        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.MissionId.Should().Be(mission.Id);
        result.UserId.Should().Be(pilotId);
        result.Status.Should().Be("CheckedIn");
    }

    [Fact]
    public async Task FieldCheckIn_Throws_WhenBeyond200Meters()
    {
        var pilotId = Guid.NewGuid();
        var userMock = CreateUserMock(pilotId);
        await using var db = CreateContext(userMock.Object);

        var tower = new Tower
        {
            Id = Guid.NewGuid(),
            TowerCode = "T-01",
            Geom = new Point(106.6297, 10.8231) { SRID = 4326 }
        };
        db.Towers.Add(tower);

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            Status = MissionStatus.Assigned,
            Assignments = new List<MissionAssignment>
            {
                new()
                {
                    UserId = pilotId,
                    AssignmentRole = "Pilot",
                    Status = MissionAssignmentStatus.Active,
                    ResponseStatus = MissionAssignmentResponse.Accepted
                }
            },
            MissionTargets = new List<MissionTarget>
            {
                new() { TowerId = tower.Id }
            }
        };
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var missionRepo = new MissionRepository(db);
        var accessFilter = new GeographicAccessFilter(db, userMock.Object);
        var towerRepo = new TowerRepository(db, accessFilter);
        var checkInRepo = new GenericRepository<MissionCheckIn>(db);
        var uow = new UnitOfWork(db);

        var handler = new FieldCheckInCommandHandler(
            missionRepo, towerRepo, checkInRepo, userMock.Object, uow);

        // Pilot is located 1.5 km away (10.8350, 106.6350)
        var pilotLat = 10.8350;
        var pilotLon = 106.6350;

        var command = new FieldCheckInCommand(mission.Id, pilotLat, pilotLon);
        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*CHECKIN_OUTSIDE_GEOFENCE*");
    }

    [Fact]
    public async Task DeliverDroneHandover_StoresChecklistAndUpdatesDroneStatus()
    {
        var managerId = Guid.NewGuid();
        var pilotId = Guid.NewGuid();
        var droneId = Guid.NewGuid();
        var userMock = CreateUserMock(managerId);
        await using var db = CreateContext(userMock.Object);

        var drone = new Uav
        {
            Id = droneId,
            UavCode = "UAV-101",
            OperationalStatus = DroneOperationalStatus.Available
        };
        db.Uavs.Add(drone);

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            UavId = droneId
        };
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var missionRepo = new MissionRepository(db);
        var uavRepo = new UavRepository(db);
        var handoverRepo = new GenericRepository<DroneHandover>(db);
        var uow = new UnitOfWork(db);

        var handler = new DeliverDroneHandoverCommandHandler(
            missionRepo, uavRepo, handoverRepo, userMock.Object, uow);

        var checklist = "{\"batteryCycles\": 12, \"propellersOk\": true, \"cameraCalibrated\": true}";
        var command = new DeliverDroneHandoverCommand(mission.Id, droneId, pilotId, "Good", checklist, true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.DroneId.Should().Be(droneId);
        result.Status.Should().Be("Accepted");

        var savedHandover = await db.DroneHandovers.FirstOrDefaultAsync(h => h.Id == result.Id);
        savedHandover.Should().NotBeNull();
        savedHandover!.InitialChecklist.Should().Be(checklist);

        var updatedDrone = await db.Uavs.FindAsync(droneId);
        updatedDrone!.OperationalStatus.Should().Be(DroneOperationalStatus.InUse);
    }

    [Fact]
    public async Task ReturnDroneHandover_SetsReturnChecklistAndReleasesDrone()
    {
        var managerId = Guid.NewGuid();
        var pilotId = Guid.NewGuid();
        var droneId = Guid.NewGuid();
        var userMock = CreateUserMock(managerId);
        await using var db = CreateContext(userMock.Object);

        var drone = new Uav
        {
            Id = droneId,
            UavCode = "UAV-101",
            OperationalStatus = DroneOperationalStatus.InUse
        };
        db.Uavs.Add(drone);

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            UavId = droneId
        };
        db.Missions.Add(mission);

        var handover = new DroneHandover
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            DroneId = droneId,
            HandedOverBy = managerId,
            ReceivedBy = pilotId,
            Status = DroneHandoverStatus.Accepted,
            InitialChecklist = "{}"
        };
        db.DroneHandovers.Add(handover);
        await db.SaveChangesAsync();

        var missionRepo = new MissionRepository(db);
        var uavRepo = new UavRepository(db);
        var handoverRepo = new GenericRepository<DroneHandover>(db);
        var uow = new UnitOfWork(db);

        var handler = new ReturnDroneHandoverCommandHandler(
            missionRepo, uavRepo, handoverRepo, uow);

        var returnChecklist = "{\"batteryRemaining\": 45, \"damageReported\": false}";
        var command = new ReturnDroneHandoverCommand(mission.Id, droneId, "Returned undamaged", returnChecklist);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.ReturnedAt.Should().NotBeNull();

        var updatedHandover = await db.DroneHandovers.FindAsync(handover.Id);
        updatedHandover!.ReturnChecklist.Should().Be(returnChecklist);

        var updatedDrone = await db.Uavs.FindAsync(droneId);
        updatedDrone!.OperationalStatus.Should().Be(DroneOperationalStatus.Available);
    }
}
