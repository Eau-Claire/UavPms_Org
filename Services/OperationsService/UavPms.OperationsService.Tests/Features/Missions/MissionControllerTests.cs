using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Features.Missions.Commands.CreateMission;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class MissionControllerTests
{
    [Fact]
    public async Task Create_MapsGisClientAliasesToExistingCommand()
    {
        var mediator = new Mock<ISender>();
        var inspectorId = Guid.NewGuid();
        var droneId = Guid.NewGuid();
        var assetIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var scheduledAt = DateTime.Parse("2026-09-03T08:00:00+07:00");
        mediator.Setup(x => x.Send(It.IsAny<CreateMissionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MissionDto());
        var controller = new MissionController(mediator.Object);
        var request = new CreateMissionRequest(null, "GIS Mission", null, null, null, null, "Description",
            null, scheduledAt, inspectorId, null, droneId, assetIds);

        var result = await controller.Create(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        mediator.Verify(x => x.Send(It.Is<CreateMissionCommand>(command =>
            command.Title == "GIS Mission" &&
            command.ScheduledStartAt == scheduledAt &&
            command.InspectorId == inspectorId &&
            command.UavId == droneId &&
            command.TargetAssetIds == assetIds), It.IsAny<CancellationToken>()), Times.Once);
    }
}
