using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assets.Commands.CreateAsset;
using UavPms.OperationsService.Application.Features.Assets.Commands.DeleteAsset;
using UavPms.OperationsService.Application.Features.Assets.Commands.UpdateAsset;
using UavPms.OperationsService.Application.Features.Assets.Queries.GetAssetById;
using UavPms.OperationsService.Application.Features.Assets.Queries.GetAssets;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Assets;

public class AssetCommandHandlerTests
{
    private readonly Mock<IAssetRepository> _assetRepositoryMock;
    private readonly Mock<ITowerRepository> _towerRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public AssetCommandHandlerTests()
    {
        _assetRepositoryMock = new Mock<IAssetRepository>();
        _towerRepositoryMock = new Mock<ITowerRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }

    #region CreateAssetCommandHandler Tests

    [Fact]
    public async Task CreateAsset_ShouldCreateAsset_WhenTowerExists()
    {
        // Arrange
        var towerId = Guid.NewGuid();
        var tower = new Tower { Id = towerId, TowerCode = "T-01", IsDeleted = false };
        _towerRepositoryMock.Setup(t => t.GetByIdAsync(towerId, true)).ReturnsAsync(tower);

        var handler = new CreateAssetCommandHandler(
            _assetRepositoryMock.Object,
            _towerRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new CreateAssetCommand(towerId, "Insulator", "INS-T01-01");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AssetCode.Should().Be("INS-T01-01");
        result.AssetType.Should().Be("Insulator");
        _assetRepositoryMock.Verify(a => a.AddAsync(It.Is<Asset>(x => x.AssetCode == "INS-T01-01")), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsset_ShouldThrowNotFoundException_WhenTowerDoesNotExist()
    {
        // Arrange
        var towerId = Guid.NewGuid();
        _towerRepositoryMock.Setup(t => t.GetByIdAsync(towerId, true)).ReturnsAsync((Tower?)null);

        var handler = new CreateAssetCommandHandler(
            _assetRepositoryMock.Object,
            _towerRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new CreateAssetCommand(towerId, "Cable", "CBL-T01-01");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region UpdateAssetCommandHandler Tests

    [Fact]
    public async Task UpdateAsset_ShouldUpdateAsset_WhenAssetAndTowerExist()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var existingAsset = new Asset { Id = assetId, TowerId = towerId, AssetCode = "OLD-CODE", IsDeleted = false };
        var tower = new Tower { Id = towerId, TowerCode = "T-01", IsDeleted = false };

        _assetRepositoryMock.Setup(a => a.GetByIdAsync(assetId, true)).ReturnsAsync(existingAsset);
        _towerRepositoryMock.Setup(t => t.GetByIdAsync(towerId, true)).ReturnsAsync(tower);

        var handler = new UpdateAssetCommandHandler(
            _assetRepositoryMock.Object,
            _towerRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new UpdateAssetCommand(assetId, towerId, "Insulator", "NEW-CODE", "Operational", 95.0, "Low Risk");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AssetCode.Should().Be("NEW-CODE");
        _assetRepositoryMock.Verify(a => a.UpdateAsync(existingAsset), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsset_ShouldThrowNotFoundException_WhenAssetDoesNotExist()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        _assetRepositoryMock.Setup(a => a.GetByIdAsync(assetId, true)).ReturnsAsync((Asset?)null);

        var handler = new UpdateAssetCommandHandler(
            _assetRepositoryMock.Object,
            _towerRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new UpdateAssetCommand(assetId, Guid.NewGuid(), "Cable", "CBL-01", "Operational", 100.0, "Low Risk");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region DeleteAssetCommandHandler Tests

    [Fact]
    public async Task DeleteAsset_ShouldDeleteAsset_WhenExists()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var asset = new Asset { Id = assetId, AssetCode = "ASSET-DELETE", IsDeleted = false };
        _assetRepositoryMock.Setup(a => a.GetByIdAsync(assetId, true)).ReturnsAsync(asset);

        var handler = new DeleteAssetCommandHandler(_assetRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new DeleteAssetCommand(assetId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _assetRepositoryMock.Verify(a => a.DeleteAsync(asset), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Query Handler Tests

    [Fact]
    public async Task GetAssetById_ShouldReturnAssetDetailWithActiveAnomalies_WhenExists()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var asset = new Asset
        {
            Id = assetId,
            TowerId = towerId,
            Tower = new Tower { Id = towerId, TowerCode = "TOWER-01" },
            AssetCode = "INS-01",
            AssetType = "Insulator",
            Status = "Operational",
            CurrentHealthScore = 88.5,
            RiskLevel = "Medium Risk",
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

        var handler = new GetAssetByIdQueryHandler(_assetRepositoryMock.Object);
        var query = new GetAssetByIdQuery(assetId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(assetId);
        result.AssetCode.Should().Be("INS-01");
        result.ActiveAnomalies.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAssets_ShouldReturnPaginatedList_WhenCalled()
    {
        // Arrange
        var assets = new List<Asset>
        {
            new Asset { Id = Guid.NewGuid(), AssetCode = "A1" },
            new Asset { Id = Guid.NewGuid(), AssetCode = "A2" }
        };
        _assetRepositoryMock.Setup(a => a.GetAssetsPagedAsync(
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
        _assetRepositoryMock.Setup(a => a.GetConfirmedDefectCountsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());

        var handler = new GetAssetsQueryHandler(_assetRepositoryMock.Object);
        var query = new GetAssetsQuery(1, 10, null, null, null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Pagination.TotalItems.Should().Be(2);
    }

    #endregion
}
