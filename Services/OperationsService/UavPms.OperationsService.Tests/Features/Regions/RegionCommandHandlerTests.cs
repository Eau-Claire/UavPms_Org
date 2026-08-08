using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Regions.Commands.CreateRegion;
using UavPms.OperationsService.Application.Features.Regions.Commands.DeleteRegion;
using UavPms.OperationsService.Application.Features.Regions.Commands.UpdateRegion;
using UavPms.OperationsService.Application.Features.Regions.Queries.GetRegionById;
using UavPms.OperationsService.Application.Features.Regions.Queries.GetRegions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Regions;

public class RegionCommandHandlerTests
{
    private readonly Mock<IRegionRepository> _regionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public RegionCommandHandlerTests()
    {
        _regionRepositoryMock = new Mock<IRegionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }

    #region CreateRegionCommandHandler Tests

    [Fact]
    public async Task CreateRegion_ShouldCreateRegionAndReturnDto_WhenRequestIsValid()
    {
        // Arrange
        var handler = new CreateRegionCommandHandler(_regionRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new CreateRegionCommand("Northern Substation Region", "Description note", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RegionName.Should().Be("Northern Substation Region");
        _regionRepositoryMock.Verify(r => r.AddAsync(It.Is<Region>(x => x.RegionName == "Northern Substation Region")), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdateRegionCommandHandler Tests

    [Fact]
    public async Task UpdateRegion_ShouldUpdateRegionName_WhenRegionExists()
    {
        // Arrange
        var regionId = Guid.NewGuid();
        var existingRegion = new Region { Id = regionId, RegionName = "Old Region Name", IsDeleted = false };
        _regionRepositoryMock.Setup(r => r.GetByIdAsync(regionId, true)).ReturnsAsync(existingRegion);

        var handler = new UpdateRegionCommandHandler(_regionRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new UpdateRegionCommand(regionId, "Updated Region Name", "New Description", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RegionName.Should().Be("Updated Region Name");
        _regionRepositoryMock.Verify(r => r.UpdateAsync(existingRegion), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRegion_ShouldThrowNotFoundException_WhenRegionDoesNotExistOrIsDeleted()
    {
        // Arrange
        var regionId = Guid.NewGuid();
        _regionRepositoryMock.Setup(r => r.GetByIdAsync(regionId, true)).ReturnsAsync((Region?)null);

        var handler = new UpdateRegionCommandHandler(_regionRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new UpdateRegionCommand(regionId, "New Name", null, null);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region DeleteRegionCommandHandler Tests

    [Fact]
    public async Task DeleteRegion_ShouldDeleteRegion_WhenRegionExists()
    {
        // Arrange
        var regionId = Guid.NewGuid();
        var existingRegion = new Region { Id = regionId, RegionName = "Region To Delete", IsDeleted = false };
        _regionRepositoryMock.Setup(r => r.GetByIdAsync(regionId, true)).ReturnsAsync(existingRegion);

        var handler = new DeleteRegionCommandHandler(_regionRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new DeleteRegionCommand(regionId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _regionRepositoryMock.Verify(r => r.DeleteAsync(existingRegion), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteRegion_ShouldThrowNotFoundException_WhenRegionDoesNotExist()
    {
        // Arrange
        var regionId = Guid.NewGuid();
        _regionRepositoryMock.Setup(r => r.GetByIdAsync(regionId, true)).ReturnsAsync((Region?)null);

        var handler = new DeleteRegionCommandHandler(_regionRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new DeleteRegionCommand(regionId);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region GetRegionByIdQueryHandler Tests

    [Fact]
    public async Task GetRegionById_ShouldReturnRegionDto_WhenRegionExists()
    {
        // Arrange
        var regionId = Guid.NewGuid();
        var region = new Region { Id = regionId, RegionName = "Hanoi Region", IsDeleted = false };
        _regionRepositoryMock.Setup(r => r.GetByIdAsync(regionId, true)).ReturnsAsync(region);

        var handler = new GetRegionByIdQueryHandler(_regionRepositoryMock.Object);
        var query = new GetRegionByIdQuery(regionId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(regionId);
        result.RegionName.Should().Be("Hanoi Region");
    }

    [Fact]
    public async Task GetRegionById_ShouldThrowNotFoundException_WhenRegionDoesNotExist()
    {
        // Arrange
        var regionId = Guid.NewGuid();
        _regionRepositoryMock.Setup(r => r.GetByIdAsync(regionId, true)).ReturnsAsync((Region?)null);

        var handler = new GetRegionByIdQueryHandler(_regionRepositoryMock.Object);
        var query = new GetRegionByIdQuery(regionId);

        // Act
        Func<Task> act = async () => await handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region GetRegionsQueryHandler Tests

    [Fact]
    public async Task GetRegions_ShouldReturnPaginatedList_WhenCalled()
    {
        // Arrange
        var regionsList = new List<Region>
        {
            new Region { Id = Guid.NewGuid(), RegionName = "Region 1" },
            new Region { Id = Guid.NewGuid(), RegionName = "Region 2" }
        };
        _regionRepositoryMock.Setup(r => r.GetRegionsPagedAsync(1, 10, null))
            .ReturnsAsync((regionsList, 2));

        var handler = new GetRegionsQueryHandler(_regionRepositoryMock.Object);
        var query = new GetRegionsQuery(1, 10, null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Pagination.TotalItems.Should().Be(2);
        result.Pagination.Page.Should().Be(1);
    }

    #endregion
}
