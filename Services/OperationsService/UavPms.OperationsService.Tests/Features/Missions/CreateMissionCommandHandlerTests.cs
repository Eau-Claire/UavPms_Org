using FluentAssertions;
using Moq;
using UavPms.Shared.Contracts.Events;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.Commands.CreateMission;
using UavPms.OperationsService.Domain.Contracts;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class CreateMissionCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _missionRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUavRepository> _uavRepositoryMock;
    private readonly Mock<ITowerRepository> _towerRepositoryMock;
    private readonly Mock<IRegionRepository> _regionRepositoryMock;
    private readonly Mock<IUserRegionAssignmentRepository> _assignmentRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly CreateMissionCommandHandler _handler;

    public CreateMissionCommandHandlerTests()
    {
        _missionRepositoryMock = new Mock<IMissionRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _uavRepositoryMock = new Mock<IUavRepository>();
        _towerRepositoryMock = new Mock<ITowerRepository>();
        _regionRepositoryMock = new Mock<IRegionRepository>();
        _assignmentRepositoryMock = new Mock<IUserRegionAssignmentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _currentUserServicesMock = new Mock<ICurrentUserServices>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        
        _handler = new CreateMissionCommandHandler(
            _missionRepositoryMock.Object,
            _userRepositoryMock.Object,
            _uavRepositoryMock.Object,
            _towerRepositoryMock.Object,
            _regionRepositoryMock.Object,
            _assignmentRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServicesMock.Object,
            _eventPublisherMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateMissionAndPublishEvent_WhenRequestIsValid()
    {
        var assignedUserId = Guid.NewGuid();
        var mockUser = new User{ Id = assignedUserId, Email = "inspector@test.com" };
        var mockUav = new Uav { Id = Guid.NewGuid(), UavCode = "XXXX" };
        var towerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        _regionRepositoryMock.Setup(x => x.GetByIdAsync(regionId, false)).ReturnsAsync(new Region { Id = regionId });
        
        _userRepositoryMock.Setup(x => x.GetByIdAsync(assignedUserId, true))
            .ReturnsAsync(mockUser);
        
        _uavRepositoryMock.Setup(x => x.GetByIdAsync(mockUav.Id, true))
            .ReturnsAsync(mockUav);
        _towerRepositoryMock.Setup(x => x.GetRegionIdsByTowerIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Guid> { [towerId] = regionId });
        _assignmentRepositoryMock.Setup(x => x.GetAssignedUserIdsAsync(regionId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { managerId, assignedUserId });
        
        _currentUserServicesMock.Setup(x => x.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(x => x.Email).Returns("admin@uavpms.com");
        
        var command = new CreateMissionCommand("Inspection A", regionId, assignedUserId, mockUav.Id, new[] { towerId }, null, "Pending", "Description");
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        result.Should().NotBeNull();
        result.Title.Should().Be("Inspection A");
        result.Status.Should().Be("Pending");
        result.RegionId.Should().Be(regionId);
        result.InspectorEmail.Should().Be("inspector@test.com");
        result.TargetTowerIds.Should().ContainSingle().Which.Should().Be(towerId);
        
        _missionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Mission>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<MissionCreatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenDroneCodeDoesNotExist()
    {
        var assignedUserId = Guid.NewGuid();
        var uavId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        _regionRepositoryMock.Setup(x => x.GetByIdAsync(regionId, false)).ReturnsAsync(new Region { Id = regionId });

        _userRepositoryMock.Setup(x => x.GetByIdAsync(assignedUserId, true))
            .ReturnsAsync(new User { Id = assignedUserId, Email = "inspector@test.com" });
        _uavRepositoryMock.Setup(x => x.GetByIdAsync(uavId, true))
            .ReturnsAsync((Uav?)null);

        var command = new CreateMissionCommand("Inspection A", regionId, assignedUserId, uavId, Array.Empty<Guid>(), null, "Pending", "Description");

        await _handler.Invoking(x => x.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();

        _uavRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Uav>()), Times.Never);
        _missionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Mission>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRejectDuplicateTargetTowerIds()
    {
        var inspectorId = Guid.NewGuid();
        var uavId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        _regionRepositoryMock.Setup(x => x.GetByIdAsync(regionId, false)).ReturnsAsync(new Region { Id = regionId });
        _userRepositoryMock.Setup(x => x.GetByIdAsync(inspectorId, true))
            .ReturnsAsync(new User { Id = inspectorId });
        _uavRepositoryMock.Setup(x => x.GetByIdAsync(uavId, true))
            .ReturnsAsync(new Uav { Id = uavId });

        var command = new CreateMissionCommand(
            "Inspection A", regionId, inspectorId, uavId, new[] { towerId, towerId }, null, "Pending", null);

        await _handler.Invoking(x => x.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<BusinessRuleException>();
    }
}
