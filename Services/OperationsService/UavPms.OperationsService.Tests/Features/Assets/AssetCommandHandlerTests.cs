using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.AssetComponents.Commands.CreateAssetComponent;
using UavPms.OperationsService.Application.Features.AssetComponents.Commands.DeleteAssetComponent;
using UavPms.OperationsService.Application.Features.AssetComponents.Commands.UpdateAssetComponent;
using UavPms.OperationsService.Application.Features.AssetComponents.Queries.GetAssetComponentById;
using UavPms.OperationsService.Application.Features.AssetComponents.Queries.GetAssetComponents;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.AssetComponents;

public class AssetCommandHandlerTests
{
    private readonly Mock<IAssetComponentRepository> _assetRepositoryMock;
    private readonly Mock<ITowerRepository> _towerRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public AssetCommandHandlerTests()
    {
        _assetRepositoryMock = new Mock<IAssetComponentRepository>();
        _towerRepositoryMock = new Mock<ITowerRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }

    #region CreateAssetComponentCommandHandler Tests

    [Fact]
    public async Task CreateAssetComponent_ShouldCreateAssetComponent_WhenTowerExists()
    {
        // Arrange
        var towerId = Guid.NewGuid();
        var tower = new Tower { Id = towerId, TowerCode = "T-01", IsDeleted = false };
        _towerRepositoryMock.Setup(t => t.GetByIdAsync(towerId, true)).ReturnsAsync(tower);

        var handler = new CreateAssetComponentCommandHandler(
            _assetRepositoryMock.Object,
            _towerRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new CreateAssetComponentCommand(towerId, "Insulator", "INS-T01-01");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ComponentCode.Should().Be("INS-T01-01");
        result.ComponentType.Should().Be("Insulator");
        _assetRepositoryMock.Verify(a => a.AddAsync(It.Is<AssetComponent>(x => x.ComponentCode == "INS-T01-01")), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAssetComponent_ShouldThrowNotFoundException_WhenTowerDoesNotExist()
    {
        // Arrange
        var towerId = Guid.NewGuid();
        _towerRepositoryMock.Setup(t => t.GetByIdAsync(towerId, true)).ReturnsAsync((Tower?)null);

        var handler = new CreateAssetComponentCommandHandler(
            _assetRepositoryMock.Object,
            _towerRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new CreateAssetComponentCommand(towerId, "Cable", "CBL-T01-01");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region UpdateAssetComponentCommandHandler Tests

    [Fact]
    public async Task UpdateAssetComponent_ShouldUpdateAssetComponent_WhenAssetAndTowerExist()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var existingAsset = new AssetComponent { Id = assetId, TowerId = towerId, ComponentCode = "OLD-CODE", IsDeleted = false };
        var tower = new Tower { Id = towerId, TowerCode = "T-01", IsDeleted = false };

        _assetRepositoryMock.Setup(a => a.GetByIdAsync(assetId, true)).ReturnsAsync(existingAsset);
        _towerRepositoryMock.Setup(t => t.GetByIdAsync(towerId, true)).ReturnsAsync(tower);

        var handler = new UpdateAssetComponentCommandHandler(
            _assetRepositoryMock.Object,
            _towerRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new UpdateAssetComponentCommand(assetId, towerId, "Insulator", "NEW-CODE", "Operational");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ComponentCode.Should().Be("NEW-CODE");
        _assetRepositoryMock.Verify(a => a.UpdateAsync(existingAsset), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAssetComponent_ShouldThrowNotFoundException_WhenAssetDoesNotExist()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        _assetRepositoryMock.Setup(a => a.GetByIdAsync(assetId, true)).ReturnsAsync((AssetComponent?)null);

        var handler = new UpdateAssetComponentCommandHandler(
            _assetRepositoryMock.Object,
            _towerRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new UpdateAssetComponentCommand(assetId, Guid.NewGuid(), "Cable", "CBL-01", "Operational");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region DeleteAssetComponentCommandHandler Tests

    [Fact]
    public async Task DeleteAssetComponent_ShouldDeleteAssetComponent_WhenExists()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var asset = new AssetComponent { Id = assetId, ComponentCode = "ASSET-DELETE", IsDeleted = false };
        _assetRepositoryMock.Setup(a => a.GetByIdAsync(assetId, true)).ReturnsAsync(asset);

        var handler = new DeleteAssetComponentCommandHandler(_assetRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new DeleteAssetComponentCommand(assetId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _assetRepositoryMock.Verify(a => a.DeleteAsync(asset), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Query Handler Tests

    [Fact]
    public async Task GetAssetComponentById_ShouldReturnAssetDetailWithActiveAnomalies_WhenExists()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var asset = new AssetComponent
        {
            Id = assetId,
            TowerId = towerId,
            Tower = new Tower { Id = towerId, TowerCode = "TOWER-01" },
            ComponentCode = "INS-01",
            ComponentType = "Insulator",
            Status = "Operational",
            IsDeleted = false,
            DetectedAnomalies = new List<DetectedAnomaly>
            {
                new DetectedAnomaly
                {
                    Id = Guid.NewGuid(),
                    ValidationStatus = "Confirmed",
                    ConfidenceScore = 0.95,
                    Category = new DefectCategory { CategoryName = "Cracked Insulator" },
                    CreatedAt = DateTime.UtcNow
                },
                new DetectedAnomaly
                {
                    Id = Guid.NewGuid(),
                    ValidationStatus = "Pending", // Should be filtered out by query handler
                    ConfidenceScore = 0.50
                }
            }
        };

        _assetRepositoryMock.Setup(a => a.GetAssetWithDetailsAsync(assetId)).ReturnsAsync(asset);

        var handler = new GetAssetComponentByIdQueryHandler(_assetRepositoryMock.Object);
        var query = new GetAssetComponentByIdQuery(assetId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(assetId);
        result.ComponentCode.Should().Be("INS-01");
        result.ActiveAnomalies.Should().HaveCount(1);
        result.ActiveAnomalies[0].CategoryName.Should().Be("Cracked Insulator");
    }

    [Fact]
    public async Task GetAssetComponents_ShouldReturnPaginatedList_WhenCalled()
    {
        // Arrange
        var assets = new List<AssetComponent>
        {
            new AssetComponent { Id = Guid.NewGuid(), ComponentCode = "A1" },
            new AssetComponent { Id = Guid.NewGuid(), ComponentCode = "A2" }
        };
        _assetRepositoryMock.Setup(a => a.GetAssetComponentsPagedAsync(
                1,
                10,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync((assets, 2));

        var handler = new GetAssetComponentsQueryHandler(_assetRepositoryMock.Object);
        var query = new GetAssetComponentsQuery(1, 10, null, null, null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Pagination.TotalItems.Should().Be(2);
    }

    #endregion
}
