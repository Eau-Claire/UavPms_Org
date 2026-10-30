using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class MissionControllerTests
{
    [Fact]
    public async Task AcceptAssignment_ReturnsMissionAssignmentResponseDto_NotRawEntity()
    {
        var mediator = new Mock<ISender>();
        var lifecycle = new Mock<IMissionLifecycleService>();
        var missionId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var assignment = new UavPms.OperationsService.Domain.Entities.MissionAssignment
        {
            Id = assignmentId,
            MissionId = missionId,
            UserId = userId,
            AssignmentRole = "INSPECTOR",
            Status = UavPms.OperationsService.Domain.Enums.MissionAssignmentStatus.Active,
            ResponseStatus = UavPms.OperationsService.Domain.Enums.MissionAssignmentResponse.Accepted,
            IsRequired = true,
            AssignedAt = DateTime.UtcNow,
            RespondedAt = DateTime.UtcNow,
            Version = 2
        };

        lifecycle.Setup(x => x.AcceptAssignmentAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignment);

        var controller = new MissionController(mediator.Object, lifecycle.Object);

        // Act
        var result = await controller.AcceptAssignment(missionId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Data.Should().BeOfType<MissionAssignmentResponseDto>();
        var dto = (MissionAssignmentResponseDto)apiResponse.Data!;
        dto.Id.Should().Be(assignmentId);
        dto.MissionId.Should().Be(missionId);
        dto.Status.Should().Be("Active");
        dto.ResponseStatus.Should().Be("Accepted");
        dto.Version.Should().Be(2);
    }

    [Fact]
    public async Task Confirm_ReturnsMissionOperationResultDto_NotRawEntity()
    {
        var mediator = new Mock<ISender>();
        var lifecycle = new Mock<IMissionLifecycleService>();
        var missionId = Guid.NewGuid();

        var mission = new UavPms.OperationsService.Domain.Entities.Mission
        {
            Id = missionId,
            MissionCode = "MSN-CONFIRMED",
            Title = "Confirmed Mission",
            Status = UavPms.OperationsService.Domain.Enums.MissionStatus.Assigned,
            Priority = UavPms.OperationsService.Domain.Enums.MissionPriority.High,
            Version = 3
        };

        lifecycle.Setup(x => x.ConfirmMissionAsync(missionId, "All ready", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mission);

        var controller = new MissionController(mediator.Object, lifecycle.Object);

        // Act
        var result = await controller.Confirm(missionId, new ConfirmMissionRequest("All ready"), CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Data.Should().BeOfType<MissionOperationResultDto>();
        var dto = (MissionOperationResultDto)apiResponse.Data!;
        dto.Id.Should().Be(missionId);
        dto.MissionCode.Should().Be("MSN-CONFIRMED");
        dto.Status.Should().Be("Assigned");
        dto.Priority.Should().Be("High");
        dto.Version.Should().Be(3);
    }
}
