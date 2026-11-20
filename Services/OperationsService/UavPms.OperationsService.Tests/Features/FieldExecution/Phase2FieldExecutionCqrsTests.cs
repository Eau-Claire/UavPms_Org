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
using UavPms.OperationsService.Application.Features.FieldExecution.Commands.FieldCheckIn;
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
            Status = MissionStatus.Confirmed,
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
            Status = MissionStatus.Confirmed,
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
}
