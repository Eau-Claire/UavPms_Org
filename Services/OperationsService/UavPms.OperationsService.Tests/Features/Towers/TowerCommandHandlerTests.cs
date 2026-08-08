using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NetTopologySuite.Geometries;
using OfficeOpenXml;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Towers.Commands.CreateTower;
using UavPms.OperationsService.Application.Features.Towers.Commands.DeleteTower;
using UavPms.OperationsService.Application.Features.Towers.Commands.ImportTowers;
using UavPms.OperationsService.Application.Features.Towers.Commands.UpdateTower;
using UavPms.OperationsService.Application.Features.Towers.Queries.GetTowerById;
using UavPms.OperationsService.Application.Features.Towers.Queries.GetTowers;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Towers;

public class TowerCommandHandlerTests
{
    private readonly Mock<ITowerRepository> _towerRepositoryMock;
    private readonly Mock<ITransmissionLineRepository> _transmissionLineRepositoryMock;
    private readonly Mock<IAssetRepository> _assetRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public TowerCommandHandlerTests()
    {
        _towerRepositoryMock = new Mock<ITowerRepository>();
        _transmissionLineRepositoryMock = new Mock<ITransmissionLineRepository>();
        _assetRepositoryMock = new Mock<IAssetRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }

    #region CreateTowerCommandHandler Tests

    [Fact]
    public async Task CreateTower_ShouldCreateTowerWithGisPoint_WhenLineExists()
    {
        // Arrange
        var lineId = Guid.NewGuid();
        var line = new TransmissionLine { Id = lineId, LineName = "500kV Line", IsDeleted = false };
        _transmissionLineRepositoryMock.Setup(l => l.GetByIdAsync(lineId, true)).ReturnsAsync(line);

        var handler = new CreateTowerCommandHandler(
            _transmissionLineRepositoryMock.Object,
            _towerRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new CreateTowerCommand(lineId, "TOWER-01", 21.0285, 105.8542);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TowerCode.Should().Be("TOWER-01");
        result.Latitude.Should().Be(21.0285);
        result.Longitude.Should().Be(105.8542);
        _towerRepositoryMock.Verify(t => t.AddAsync(It.Is<Tower>(x => x.TowerCode == "TOWER-01" && x.Geom != null && x.Geom.SRID == 4326)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTower_ShouldThrowNotFoundException_WhenLineDoesNotExist()
    {
        // Arrange
        var lineId = Guid.NewGuid();
        _transmissionLineRepositoryMock.Setup(l => l.GetByIdAsync(lineId, true)).ReturnsAsync((TransmissionLine?)null);

        var handler = new CreateTowerCommandHandler(
            _transmissionLineRepositoryMock.Object,
            _towerRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new CreateTowerCommand(lineId, "TOWER-99", 21.0, 105.0);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region UpdateTowerCommandHandler Tests

    [Fact]
    public async Task UpdateTower_ShouldUpdateTowerAndGis_WhenTowerAndLineExist()
    {
        // Arrange
        var towerId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var existingTower = new Tower { Id = towerId, LineAssetId = lineId, TowerCode = "OLD-CODE", IsDeleted = false };
        var line = new TransmissionLine { Id = lineId, LineName = "Line 1", IsDeleted = false };

        _towerRepositoryMock.Setup(t => t.GetByIdAsync(towerId, true)).ReturnsAsync(existingTower);
        _transmissionLineRepositoryMock.Setup(l => l.GetByIdAsync(lineId, true)).ReturnsAsync(line);

        var handler = new UpdateTowerCommandHandler(
            _towerRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new UpdateTowerCommand(towerId, lineId, "NEW-CODE", 21.05, 105.90);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TowerCode.Should().Be("NEW-CODE");
        _towerRepositoryMock.Verify(t => t.UpdateAsync(existingTower), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DeleteTowerCommandHandler Tests

    [Fact]
    public async Task DeleteTower_ShouldSetIsDeleted_WhenTowerExists()
    {
        // Arrange
        var towerId = Guid.NewGuid();
        var tower = new Tower { Id = towerId, TowerCode = "TOWER-DELETE", IsDeleted = false };
        _towerRepositoryMock.Setup(t => t.GetByIdAsync(towerId, true)).ReturnsAsync(tower);

        var handler = new DeleteTowerCommandHandler(_towerRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new DeleteTowerCommand(towerId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        tower.IsDeleted.Should().BeTrue();
        _towerRepositoryMock.Verify(t => t.UpdateAsync(tower), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ImportTowerCommandHandler Tests (Batch Import & Auto Asset Creation)

    [Fact]
    public async Task ImportTower_ShouldParseExcelAndCreateTowersAndAutoCreateAssets()
    {
        // Arrange
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var lineId = Guid.NewGuid();
        var line = new TransmissionLine { Id = lineId, LineName = "Import Test Line", IsDeleted = false };
        _transmissionLineRepositoryMock.Setup(l => l.GetByIdAsync(lineId, true)).ReturnsAsync(line);

        // Build Excel File Stream with 2 valid rows
        using var stream = new MemoryStream();
        using (var package = new ExcelPackage(stream))
        {
            var worksheet = package.Workbook.Worksheets.Add("Towers");
            // Header Row
            worksheet.Cells[1, 1].Value = "LineAssetId";
            worksheet.Cells[1, 2].Value = "TowerCode";
            worksheet.Cells[1, 3].Value = "Latitude";
            worksheet.Cells[1, 4].Value = "Longitude";

            // Row 2
            worksheet.Cells[2, 1].Value = lineId.ToString();
            worksheet.Cells[2, 2].Value = "T-01";
            worksheet.Cells[2, 3].Value = "21.0285";
            worksheet.Cells[2, 4].Value = "105.8542";

            // Row 3
            worksheet.Cells[3, 1].Value = lineId.ToString();
            worksheet.Cells[3, 2].Value = "T-02";
            worksheet.Cells[3, 3].Value = "21.0290";
            worksheet.Cells[3, 4].Value = "105.8550";

            package.Save();
        }
        stream.Position = 0;

        var handler = new ImportTowerCommandHandler(
            _towerRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _assetRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object);

        var command = new ImportTowersCommand(stream);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ImportedCount.Should().Be(2); // 2 towers imported
        result.CreatedAssetsCount.Should().Be(8); // 4 assets per tower * 2 towers = 8 assets

        _towerRepositoryMock.Verify(t => t.AddAsync(It.IsAny<Tower>()), Times.Exactly(2));
        _assetRepositoryMock.Verify(a => a.AddAsync(It.IsAny<Asset>()), Times.Exactly(8));
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Query Handler Tests

    [Fact]
    public async Task GetTowerById_ShouldReturnTowerDto_WhenExists()
    {
        // Arrange
        var towerId = Guid.NewGuid();
        var tower = new Tower
        {
            Id = towerId,
            TowerCode = "T-10",
            Geom = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326).CreatePoint(new Coordinate(105.8, 21.0)),
            IsDeleted = false
        };
        _towerRepositoryMock.Setup(t => t.GetByIdAsync(towerId, true)).ReturnsAsync(tower);

        var handler = new GetTowerByIQueryHandler(_towerRepositoryMock.Object);
        var query = new GetTowerByIdQuery(towerId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(towerId);
        result.TowerCode.Should().Be("T-10");
        result.Latitude.Should().Be(21.0);
        result.Longitude.Should().Be(105.8);
    }

    [Fact]
    public async Task GetTowers_ShouldReturnPaginatedList_WhenCalled()
    {
        // Arrange
        var towers = new List<Tower>
        {
            new Tower { Id = Guid.NewGuid(), TowerCode = "T1" },
            new Tower { Id = Guid.NewGuid(), TowerCode = "T2" }
        };
        _towerRepositoryMock.Setup(t => t.GetTowersPagedAsync(1, 10, null))
            .ReturnsAsync((towers, 2));

        var handler = new GetTowersQueryHandler(_towerRepositoryMock.Object);
        var query = new GetTowersQuery(1, 10, null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Pagination.TotalItems.Should().Be(2);
    }

    #endregion
}
