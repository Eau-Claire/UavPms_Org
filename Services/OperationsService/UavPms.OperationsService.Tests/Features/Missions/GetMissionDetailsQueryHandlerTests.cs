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
    public async Task Handle_ReturnsAssetTargetsInSequenceOrder()
    {
        var missionId = Guid.NewGuid();
        var asset = new Asset { Id = Guid.NewGuid(), AssetCode = "A-1", AssetType = "Insulator", Location = new Point(106.8, 10.84) { SRID = 4326 } };
        var mission = new Mission { Id = missionId, MissionTargets = new List<MissionTarget>
        {
            new() { AssetId = asset.Id, Asset = asset, Sequence = 2, InspectionStatus = MissionTargetInspectionStatus.Pending }
        }};
        var repository = new Mock<IMissionRepository>();
        repository.Setup(x => x.GetMissionDetailsByIdAsync(missionId)).ReturnsAsync(mission);

        var result = await new GetMissionDetailsQueryHandler(repository.Object).Handle(new GetMissionDetailsQuery(missionId), CancellationToken.None);

        result.Targets.Should().ContainSingle(x => x.AssetId == asset.Id && x.AssetCode == "A-1" && x.Sequence == 2 && x.Latitude == 10.84);
    }
}
