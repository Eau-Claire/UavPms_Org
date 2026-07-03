using FluentAssertions;
using Moq;
using UavPms.Application.Features.Missions.Queries.GetMyMissions;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.UnitTests.Features.Missions;

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
            new() { Id = Guid.NewGuid(), Title = "Mission 1", AssignedToUserId = currentUserId, Status = "Pending" },
            new() { Id = Guid.NewGuid(), Title = "Mission 2", AssignedToUserId = currentUserId, Status = "In Progress" }
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
}