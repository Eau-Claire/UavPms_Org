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
}