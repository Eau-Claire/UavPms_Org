using System.Linq.Expressions;
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
    private readonly Mock<ITowerRepository> _assetRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly CreateMissionCommandHandler _handler;

    public CreateMissionCommandHandlerTests()
    {
        _missionRepositoryMock = new Mock<IMissionRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _uavRepositoryMock = new Mock<IUavRepository>();
        _assetRepositoryMock = new Mock<ITowerRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _currentUserServicesMock = new Mock<ICurrentUserServices>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        
        _handler = new CreateMissionCommandHandler(
            _missionRepositoryMock.Object,
            _userRepositoryMock.Object,
            _uavRepositoryMock.Object,
            _assetRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServicesMock.Object,
            _eventPublisherMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateMissionAndPublishEvent_WhenRequestIsValid()
    {
        var assignedUserId = Guid.NewGuid();
        var mockUser = new User
        {
            Id = assignedUserId,
            Email = "inspector@test.com",
            UserRoles = new List<UserRole>
            {
                new() { Role = new Role { RoleName = "Inspector" } }
            }
        };
        var droneCode = "XXXX";
        var mockUav = new Uav { Id = Guid.NewGuid(), UavCode = droneCode };
        var targetTowerId = Guid.NewGuid();
        var targetAssets = new List<Tower>
        {
            new() { Id = targetTowerId, RegionId = Guid.NewGuid(), TowerCode = "TOW-N1-01" },
            new() { Id = Guid.NewGuid(), RegionId = Guid.NewGuid(), TowerCode = "TOW-N1-02" }
        };
        
        _userRepositoryMock.Setup(x => x.GetByIdWithRolesAsync(assignedUserId))
            .ReturnsAsync(mockUser);
        
        _uavRepositoryMock.Setup(x => x.GetByUavCodeAsync(droneCode))
            .ReturnsAsync(mockUav);

        _assetRepositoryMock.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Tower, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync((Expression<Func<Tower, bool>> predicate, bool _) => targetAssets.Where(predicate.Compile()).ToList());
        
        _currentUserServicesMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _currentUserServicesMock.Setup(x => x.Email).Returns("admin@uavpms.com");
        
        var command = new CreateMissionCommand(
            "Inspection A",
            "Route abc",
            assignedUserId,
            droneCode,
            "Pending",
            "Description",
            TargetTowerIds: new[] { targetTowerId });
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        result.Should().NotBeNull();
        result.Title.Should().Be("Inspection A");
        result.Status.Should().Be("Draft");
        result.AssignedToEmail.Should().Be("inspector@test.com");
        
        _missionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Mission>()), Times.Once);
        result.Targets.Should().ContainSingle(target =>
            target.TowerId == targetTowerId &&
            target.Sequence == 1);

        _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<MissionCreatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRejectMissionWithoutTargets()
    {
        var command = new CreateMissionCommand("Mission", null, Guid.NewGuid(), "UAV-1", null, null);
        await _handler.Invoking(x => x.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("MISSION_TARGET_REQUIRED");
    }

    [Fact]
    public async Task Handle_ShouldRejectDuplicateTargets()
    {
        var id = Guid.NewGuid();
        var command = new CreateMissionCommand("Mission", null, Guid.NewGuid(), "UAV-1", null, null, TargetTowerIds: new[] { id, id });
        await _handler.Invoking(x => x.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Handle_ShouldRejectMissingAsset()
    {
        var (command, user, uav) = ValidDependencies();
        _userRepositoryMock.Setup(x => x.GetByIdWithRolesAsync(command.AssignedToUserId)).ReturnsAsync(user);
        _uavRepositoryMock.Setup(x => x.GetByUavCodeAsync(command.DroneCode!)).ReturnsAsync(uav);
        _assetRepositoryMock.Setup(x => x.FindAsync(It.IsAny<Expression<Func<Tower, bool>>>(), It.IsAny<bool>())).ReturnsAsync(Array.Empty<Tower>());
        await _handler.Invoking(x => x.Handle(command, CancellationToken.None)).Should().ThrowAsync<NotFoundException>();
    }

    private static (CreateMissionCommand Command, User User, Uav Uav) ValidDependencies()
    {
        var inspectorId = Guid.NewGuid();
        var user = new User { Id = inspectorId, UserRoles = new[] { new UserRole { Role = new Role { RoleName = "Inspector" } } } };
        var uav = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-1" };
        return (new CreateMissionCommand("Mission", null, inspectorId, uav.UavCode, null, null, TargetTowerIds: new[] { Guid.NewGuid() }), user, uav);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenDroneCodeDoesNotExist()
    {
        var assignedUserId = Guid.NewGuid();
        var droneCode = "UAV-999";

        _userRepositoryMock.Setup(x => x.GetByIdWithRolesAsync(assignedUserId))
            .ReturnsAsync(new User
            {
                Id = assignedUserId,
                Email = "inspector@test.com",
                UserRoles = new List<UserRole>
                {
                    new() { Role = new Role { RoleName = "Inspector" } }
                }
            });
        _uavRepositoryMock.Setup(x => x.GetByUavCodeAsync(droneCode))
            .ReturnsAsync((Uav?)null);

        var command = new CreateMissionCommand(
            "Inspection A",
            "Route abc",
            assignedUserId,
            droneCode,
            "Pending",
            "Description",
            TargetTowerIds: new[] { Guid.NewGuid() });

        await _handler.Invoking(x => x.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();

        _uavRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Uav>()), Times.Never);
        _missionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Mission>()), Times.Never);
    }
}
