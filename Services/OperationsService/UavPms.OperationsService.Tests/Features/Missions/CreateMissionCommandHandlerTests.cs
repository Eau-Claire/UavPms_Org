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
    private readonly Mock<IAssetRepository> _assetRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly CreateMissionCommandHandler _handler;

    public CreateMissionCommandHandlerTests()
    {
        _missionRepositoryMock = new Mock<IMissionRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _uavRepositoryMock = new Mock<IUavRepository>();
        _assetRepositoryMock = new Mock<IAssetRepository>();
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
        var targetAssetId = Guid.NewGuid();
        var targetTowerId = Guid.NewGuid();
        var targetAssets = new List<Asset>
        {
            new()
            {
                Id = targetAssetId,
                TowerId = targetTowerId,
                Tower = new Tower { Id = targetTowerId, TowerCode = "TOW-N1-01" },
                AssetCode = "INS-TOW01-01",
                Status = "Active"
            }
        };
        
        _userRepositoryMock.Setup(x => x.GetByIdWithRolesAsync(assignedUserId))
            .ReturnsAsync(mockUser);
        
        _uavRepositoryMock.Setup(x => x.GetByUavCodeAsync(droneCode))
            .ReturnsAsync(mockUav);

        _assetRepositoryMock.Setup(x => x.GetAssetsByIdsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { targetAssetId })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetAssets);
        
        _currentUserServicesMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _currentUserServicesMock.Setup(x => x.Email).Returns("admin@uavpms.com");
        
        var command = new CreateMissionCommand(
            "Inspection A",
            "Route abc",
            assignedUserId,
            droneCode,
            "Pending",
            "Description",
            TargetAssetIds: new[] { targetAssetId });
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        result.Should().NotBeNull();
        result.Title.Should().Be("Inspection A");
        result.Status.Should().Be("Pending");
        result.AssignedToEmail.Should().Be("inspector@test.com");
        
        _missionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Mission>()), Times.Once);
        result.Targets.Should().ContainSingle(target =>
            target.AssetId == targetAssetId &&
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
        var command = new CreateMissionCommand("Mission", null, Guid.NewGuid(), "UAV-1", null, null, TargetAssetIds: new[] { id, id });
        await _handler.Invoking(x => x.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Handle_ShouldRejectMissingAsset()
    {
        var (command, user, uav) = ValidDependencies();
        _userRepositoryMock.Setup(x => x.GetByIdWithRolesAsync(command.AssignedToUserId)).ReturnsAsync(user);
        _uavRepositoryMock.Setup(x => x.GetByUavCodeAsync(command.DroneCode!)).ReturnsAsync(uav);
        _assetRepositoryMock.Setup(x => x.GetAssetsByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Asset>());
        await _handler.Invoking(x => x.Handle(command, CancellationToken.None)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldRejectInactiveAsset()
    {
        var (command, user, uav) = ValidDependencies();
        _userRepositoryMock.Setup(x => x.GetByIdWithRolesAsync(command.AssignedToUserId)).ReturnsAsync(user);
        _uavRepositoryMock.Setup(x => x.GetByUavCodeAsync(command.DroneCode!)).ReturnsAsync(uav);
        _assetRepositoryMock.Setup(x => x.GetAssetsByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Asset { Id = command.TargetAssetIds![0], Status = "Inactive" } });
        await _handler.Invoking(x => x.Handle(command, CancellationToken.None)).Should().ThrowAsync<BusinessRuleException>().WithMessage("ASSET_NOT_AVAILABLE");
    }

    private static (CreateMissionCommand Command, User User, Uav Uav) ValidDependencies()
    {
        var inspectorId = Guid.NewGuid();
        var user = new User { Id = inspectorId, UserRoles = new[] { new UserRole { Role = new Role { RoleName = "Inspector" } } } };
        var uav = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-1" };
        return (new CreateMissionCommand("Mission", null, inspectorId, uav.UavCode, null, null, TargetAssetIds: new[] { Guid.NewGuid() }), user, uav);
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
            TargetAssetIds: new[] { Guid.NewGuid() });

        await _handler.Invoking(x => x.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();

        _uavRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Uav>()), Times.Never);
        _missionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Mission>()), Times.Never);
    }
}
