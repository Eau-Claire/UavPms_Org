using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Features.Missions.Queries.ListMissions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class ListMissionsGeographicScopeTests
{
    private readonly Mock<IMissionRepository> _missionRepoMock;
    private readonly Mock<ICurrentUserServices> _currentUserMock;
    private readonly Mock<IGenericRepository<UserGeographicScope>> _scopeRepoMock;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _regionId = Guid.NewGuid();

    public ListMissionsGeographicScopeTests()
    {
        _missionRepoMock = new Mock<IMissionRepository>();
        _currentUserMock = new Mock<ICurrentUserServices>();
        _scopeRepoMock = new Mock<IGenericRepository<UserGeographicScope>>();

        _currentUserMock.Setup(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(c => c.UserId).Returns(_userId);
    }

    [Fact]
    public async Task Handle_ShouldPassAllowedRegionIdsToRepository_WhenUserIsRegionalManager()
    {
        // Arrange
        _currentUserMock.Setup(c => c.Roles).Returns(new[] { UserRoles.Manager });

        _scopeRepoMock.Setup(s => s.FindAsync(
                It.IsAny<Expression<Func<UserGeographicScope, bool>>>(),
                false))
            .ReturnsAsync(new List<UserGeographicScope>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    UserId = _userId,
                    RegionId = _regionId
                }
            });

        IReadOnlyList<Guid?>? capturedRegionIds = null;

        _missionRepoMock.Setup(m => m.GetMissionsPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<Guid?>?>()))
            .Callback<int, int, string?, string?, string?, bool, string?, IReadOnlyList<Guid?>?>(
                (page, pageSize, search, status, sortBy, desc, priority, regionIds) =>
                {
                    capturedRegionIds = regionIds;
                })
            .ReturnsAsync((new List<Mission>(), 0));

        var handler = new ListMissionsQueryHandler(
            _missionRepoMock.Object,
            _currentUserMock.Object,
            _scopeRepoMock.Object);

        // Act
        var result = await handler.Handle(new ListMissionsQuery(1, 10, null, null), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        capturedRegionIds.Should().NotBeNull();
        capturedRegionIds.Should().ContainSingle().Which.Should().Be(_regionId);
    }

    [Fact]
    public async Task Handle_ShouldPassNullAllowedRegionIds_WhenUserIsSystemAdmin()
    {
        // Arrange
        _currentUserMock.Setup(c => c.Roles).Returns(new[] { UserRoles.SystemAdmin });

        IReadOnlyList<Guid?>? capturedRegionIds = null;

        _missionRepoMock.Setup(m => m.GetMissionsPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<Guid?>?>()))
            .Callback<int, int, string?, string?, string?, bool, string?, IReadOnlyList<Guid?>?>(
                (page, pageSize, search, status, sortBy, desc, priority, regionIds) =>
                {
                    capturedRegionIds = regionIds;
                })
            .ReturnsAsync((new List<Mission>(), 0));

        var handler = new ListMissionsQueryHandler(
            _missionRepoMock.Object,
            _currentUserMock.Object,
            _scopeRepoMock.Object);

        // Act
        var result = await handler.Handle(new ListMissionsQuery(1, 10, null, null), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        capturedRegionIds.Should().BeNull();
    }
}
