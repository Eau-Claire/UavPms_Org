using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Regions.UserAssignments;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Tests.Features.Regions;

public class UserRegionAssignmentHandlerTests
{
    [Fact]
    public async Task Assign_Succeeds()
    {
        var regionId = Guid.NewGuid(); var userId = Guid.NewGuid();
        var regions = new Mock<IRegionRepository>(); var users = new Mock<IUserRepository>();
        var assignments = new Mock<IUserRegionAssignmentRepository>(); var current = new Mock<ICurrentUserServices>(); var unit = new Mock<IUnitOfWork>();
        regions.Setup(x => x.GetByIdAsync(regionId, false)).ReturnsAsync(new Region { Id = regionId });
        users.Setup(x => x.GetByIdAsync(userId, false)).ReturnsAsync(new User { Id = userId });
        current.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        await new AssignUserToRegionCommandHandler(regions.Object, users.Object, assignments.Object, current.Object, unit.Object)
            .Handle(new AssignUserToRegionCommand(userId, regionId), default);
        assignments.Verify(x => x.AddAsync(It.Is<UserRegionAssignment>(a => a.UserId == userId && a.RegionId == regionId), It.IsAny<CancellationToken>()), Times.Once);
        unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Assign_Duplicate_IsRejected()
    {
        var regionId = Guid.NewGuid(); var userId = Guid.NewGuid();
        var regions = new Mock<IRegionRepository>(); var users = new Mock<IUserRepository>(); var assignments = new Mock<IUserRegionAssignmentRepository>();
        regions.Setup(x => x.GetByIdAsync(regionId, false)).ReturnsAsync(new Region { Id = regionId });
        users.Setup(x => x.GetByIdAsync(userId, false)).ReturnsAsync(new User { Id = userId });
        assignments.Setup(x => x.ExistsAsync(userId, regionId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new AssignUserToRegionCommandHandler(regions.Object, users.Object, assignments.Object, Mock.Of<ICurrentUserServices>(), Mock.Of<IUnitOfWork>());
        await handler.Invoking(x => x.Handle(new AssignUserToRegionCommand(userId, regionId), default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Remove_Succeeds()
    {
        var regionId = Guid.NewGuid(); var userId = Guid.NewGuid(); var assignment = new UserRegionAssignment { UserId = userId, RegionId = regionId };
        var assignments = new Mock<IUserRegionAssignmentRepository>(); var unit = new Mock<IUnitOfWork>();
        assignments.Setup(x => x.GetAsync(userId, regionId, It.IsAny<CancellationToken>())).ReturnsAsync(assignment);
        await new RemoveUserFromRegionCommandHandler(assignments.Object, unit.Object).Handle(new RemoveUserFromRegionCommand(userId, regionId), default);
        assignments.Verify(x => x.Remove(assignment), Times.Once);
        unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
