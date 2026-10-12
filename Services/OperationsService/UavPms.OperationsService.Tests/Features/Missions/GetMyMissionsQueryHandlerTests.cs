using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Features.Missions.Queries.GetMyMissions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class GetMyMissionsQueryHandlerTests
{
    private readonly Mock<IMissionRepository> _missionRepoMock;
    private readonly Mock<ICurrentUserServices> _currentUserMock;
    private readonly GetMyMissionsQueryHandler _handler;

    public GetMyMissionsQueryHandlerTests()
    {
        _missionRepoMock = new Mock<IMissionRepository>();
        _currentUserMock = new Mock<ICurrentUserServices>();
        _handler = new GetMyMissionsQueryHandler(_missionRepoMock.Object, _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnAssignedMissions_ForCurrentUser()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        _currentUserMock.Setup(x => x.UserId).Returns(currentUserId);

        var mockMissions = new List<Mission>
        {
            new() { Id = Guid.NewGuid(), Title = "Mission 1", AssignedToUserId = currentUserId, Status = UavPms.OperationsService.Domain.Enums.MissionStatus.Pending },
            new() { Id = Guid.NewGuid(), Title = "Mission 2", AssignedToUserId = currentUserId, Status = UavPms.OperationsService.Domain.Enums.MissionStatus.Executing }
        };

        _missionRepoMock.Setup(x => x.GetMissionsByAssignedUserAsync(currentUserId))
            .ReturnsAsync(mockMissions);

        var query = new GetMyMissionsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Mission 1");
    }

    [Fact]
    public async Task Handle_ShouldPopulateNewWorkflowFields_WhenMissionHasData()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var droneId = Guid.NewGuid();
        var deadline = DateTime.UtcNow.AddDays(1);
        _currentUserMock.Setup(x => x.UserId).Returns(currentUserId);

        var mockMissions = new List<Mission>
        {
            new()
            {
                Id = Guid.NewGuid(),
                MissionCode = "MSN-001",
                Title = "Survey Mission",
                AssignedToUserId = currentUserId,
                Status = UavPms.OperationsService.Domain.Enums.MissionStatus.PendingAcceptance,
                ConfirmationDeadline = deadline,
                ManagerInstructions = "Check tower 15",
                UavId = droneId,
                DroneCode = "DRN-M300",
                Inspector = new User { Id = currentUserId, FullName = "Inspector John", Email = "john@example.com" }
            }
        };

        _missionRepoMock.Setup(x => x.GetMissionsByAssignedUserAsync(currentUserId))
            .ReturnsAsync(mockMissions);

        // Act
        var result = await _handler.Handle(new GetMyMissionsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.MissionCode.Should().Be("MSN-001");
        dto.ConfirmationDeadline.Should().Be(deadline);
        dto.ManagerInstructions.Should().Be("Check tower 15");
        dto.DroneCode.Should().Be("DRN-M300");
        dto.DroneId.Should().Be(droneId);
        dto.AssignedToUsername.Should().Be("Inspector John");
    }

    [Fact]
    public async Task Handle_ShouldFilterOutRevokedAssignmentsFromTeam()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        _currentUserMock.Setup(x => x.UserId).Returns(currentUserId);

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MSN-FILTER",
            Title = "Mission With Assignments",
            ManagerId = Guid.NewGuid(),
            Status = UavPms.OperationsService.Domain.Enums.MissionStatus.Assigned
        };

        var activeAssignment = new MissionAssignment
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            AssignmentRole = "PILOT",
            Status = UavPms.OperationsService.Domain.Enums.MissionAssignmentStatus.Active,
            ResponseStatus = UavPms.OperationsService.Domain.Enums.MissionAssignmentResponse.Accepted,
            User = new User { Id = currentUserId, FullName = "Active Pilot" }
        };

        var revokedAssignment = new MissionAssignment
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            AssignmentRole = "OBSERVER",
            Status = UavPms.OperationsService.Domain.Enums.MissionAssignmentStatus.Revoked,
            ResponseStatus = UavPms.OperationsService.Domain.Enums.MissionAssignmentResponse.Pending,
            User = new User { Id = otherUserId, FullName = "Revoked Observer" }
        };

        mission.Assignments.Add(activeAssignment);
        mission.Assignments.Add(revokedAssignment);

        _missionRepoMock.Setup(x => x.GetMissionsByAssignedUserAsync(currentUserId))
            .ReturnsAsync(new List<Mission> { mission });

        // Act
        var result = await _handler.Handle(new GetMyMissionsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Team.Should().HaveCount(1);
        result[0].Team[0].UserId.Should().Be(currentUserId);
        result[0].Team[0].Status.Should().Be("Active");
    }
}