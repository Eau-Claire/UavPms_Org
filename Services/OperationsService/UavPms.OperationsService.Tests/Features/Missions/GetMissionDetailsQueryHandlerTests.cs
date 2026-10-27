using FluentAssertions;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Features.Missions.Queries.GetMissionDetails;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class GetMissionDetailsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTowerTargetsInSequenceOrder()
    {
        var missionId = Guid.NewGuid();
        var tower = new Tower { Id = Guid.NewGuid(), RegionId = Guid.NewGuid(), TowerCode = "A-1", Geom = new Point(106.8, 10.84) { SRID = 4326 } };
        var mission = new Mission { Id = missionId, MissionTargets = new List<MissionTarget>
        {
            new() { TowerId = tower.Id, Tower = tower, Sequence = 2, InspectionStatus = MissionTargetInspectionStatus.Pending }
        }};
        var repository = new Mock<IMissionRepository>();
        repository.Setup(x => x.GetMissionDetailsByIdAsync(missionId)).ReturnsAsync(mission);

        var result = await new GetMissionDetailsQueryHandler(repository.Object).Handle(new GetMissionDetailsQuery(missionId), CancellationToken.None);

        result.Targets.Should().ContainSingle(x => x.TowerId == tower.Id && x.TowerCode == "A-1" && x.Sequence == 2 && x.Latitude == 10.84);
    }
}
