using FluentAssertions;
using Moq;
using UavPms.Application.Features.Missions.Commands.CreateMission;
using UavPms.Core.Contracts;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.UnitTests.Features.Missions;

public class CreateMissionCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _missionRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUavRepository> _uavRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly CreateMissionCommandHandler _handler;

    public CreateMissionCommandHandlerTests()
    {
        _missionRepositoryMock = new Mock<IMissionRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _uavRepositoryMock = new Mock<IUavRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _currentUserServicesMock = new Mock<ICurrentUserServices>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        
        _handler = new CreateMissionCommandHandler(
            _missionRepositoryMock.Object,
            _userRepositoryMock.Object,
            _uavRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServicesMock.Object,
            _eventPublisherMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateMissionAndPublishEvent_WhenRequestIsValid()
    {
        var assignedUserId = Guid.NewGuid();
        var mockUser = new User{ Id = assignedUserId, Username = "inspector" };
        var droneCode = "XXXX";
        var mockUav = new Uav { Id = Guid.NewGuid(), UavCode = droneCode };
        
        _userRepositoryMock.Setup(x => x.GetByIdAsync(assignedUserId, true))
            .ReturnsAsync(mockUser);
        
        _uavRepositoryMock.Setup(x => x.GetByUavCodeAsync(droneCode))
            .ReturnsAsync(mockUav);
        
        _currentUserServicesMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _currentUserServicesMock.Setup(x => x.UserName).Returns("admin");
        
        var command = new CreateMissionCommand("Inspection A", "Route abc", assignedUserId, droneCode, "Pending", "Description");
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        result.Should().NotBeNull();
        result.Title.Should().Be("Inspection A");
        result.Status.Should().Be("Pending");
        result.AssignedToUsername.Should().Be("inspector");
        
        _missionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Mission>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<MissionCreatedEvent>()), Times.Once);
    }
}