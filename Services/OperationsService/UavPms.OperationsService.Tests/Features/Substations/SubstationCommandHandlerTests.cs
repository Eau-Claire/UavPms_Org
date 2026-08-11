using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Substations.Commands.CreateSubstation;
using UavPms.OperationsService.Application.Features.Substations.Commands.DeleteSubstation;
using UavPms.OperationsService.Application.Features.Substations.Commands.UpdateSubstation;
using UavPms.OperationsService.Application.Features.Substations.Queries.GetSubstation;
using UavPms.OperationsService.Application.Features.Substations.Queries.GetSubstationById;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Substations;

public class SubstationCommandHandlerTests
{
    private readonly Mock<ISubstationRepository> _substationRepositoryMock;
    private readonly Mock<IRegionRepository> _regionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public SubstationCommandHandlerTests()
    {
        _substationRepositoryMock = new Mock<ISubstationRepository>();
        _regionRepositoryMock = new Mock<IRegionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }

    #region CreateSubstationCommandHandler Tests

    [Fact]
    public async Task CreateSubstation_ShouldCreateSubstation_WhenRegionExists()
    {
        // Arrange
        var regionId = Guid.NewGuid();
        var region = new Region { Id = regionId, RegionName = "Test Region", IsDeleted = false };
        _regionRepositoryMock.Setup(r => r.GetByIdAsync(regionId, true)).ReturnsAsync(region);

        var handler = new CreateSubstationCommandHandler(
            _substationRepositoryMock.Object,
            _regionRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new CreateSubstationCommand(regionId, "Substation 500kV", "500kV", 21.02, 105.85);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SubstationName.Should().Be("Substation 500kV");
        _substationRepositoryMock.Verify(s => s.AddAsync(It.Is<Substation>(x => x.SubstationName == "Substation 500kV")), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSubstation_ShouldThrowNotFoundException_WhenRegionDoesNotExist()
    {
        // Arrange
        var regionId = Guid.NewGuid();
        _regionRepositoryMock.Setup(r => r.GetByIdAsync(regionId, true)).ReturnsAsync((Region?)null);

        var handler = new CreateSubstationCommandHandler(
            _substationRepositoryMock.Object,
            _regionRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new CreateSubstationCommand(regionId, "Substation 220kV", "220kV", null, null);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region UpdateSubstationCommandHandler Tests

    [Fact]
    public async Task UpdateSubstation_ShouldUpdateSubstation_WhenSubstationAndRegionExist()
    {
        // Arrange
        var substationId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var existingSubstation = new Substation { Id = substationId, SubstationName = "Old Substation", RegionAssetId = regionId, IsDeleted = false };
        var region = new Region { Id = regionId, RegionName = "Region A", IsDeleted = false };

        _substationRepositoryMock.Setup(s => s.GetByIdAsync(substationId, true)).ReturnsAsync(existingSubstation);
        _regionRepositoryMock.Setup(r => r.GetByIdAsync(regionId, true)).ReturnsAsync(region);

        var handler = new UpdateSubstationCommandHandler(
            _substationRepositoryMock.Object,
            _regionRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new UpdateSubstationCommand(substationId, regionId, "New Substation Name", "110kV", 21.03, 105.86);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SubstationName.Should().Be("New Substation Name");
        _substationRepositoryMock.Verify(s => s.UpdateAsync(existingSubstation), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSubstation_ShouldThrowNotFoundException_WhenSubstationDoesNotExist()
    {
        // Arrange
        var substationId = Guid.NewGuid();
        _substationRepositoryMock.Setup(s => s.GetByIdAsync(substationId, true)).ReturnsAsync((Substation?)null);

        var handler = new UpdateSubstationCommandHandler(
            _substationRepositoryMock.Object,
            _regionRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new UpdateSubstationCommand(substationId, Guid.NewGuid(), "Substation", "110kV", null, null);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region DeleteSubstationCommandHandler Tests

    [Fact]
    public async Task DeleteSubstation_ShouldSoftDeleteSubstation_WhenExists()
    {
        // Arrange
        var substationId = Guid.NewGuid();
        var substation = new Substation { Id = substationId, SubstationName = "Substation to delete", IsDeleted = false };
        _substationRepositoryMock.Setup(s => s.GetByIdAsync(substationId, true)).ReturnsAsync(substation);

        var handler = new DeleteSubstationCommandHandler(_substationRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new DeleteSubstationCommand(substationId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        substation.IsDeleted.Should().BeTrue();
        _substationRepositoryMock.Verify(s => s.UpdateAsync(substation), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Query Handler Tests

    [Fact]
    public async Task GetSubstationById_ShouldReturnDto_WhenExists()
    {
        // Arrange
        var substationId = Guid.NewGuid();
        var substation = new Substation { Id = substationId, SubstationName = "Substation 1", VoltageLevel = "220kV", IsDeleted = false };
        _substationRepositoryMock.Setup(s => s.GetByIdAsync(substationId, true)).ReturnsAsync(substation);

        var handler = new GetSubstationByIdQueryHandler(_substationRepositoryMock.Object);
        var query = new GetSubstationByIdQuery(substationId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(substationId);
    }

    [Fact]
    public async Task GetSubstations_ShouldReturnPaginatedList_WhenCalled()
    {
        // Arrange
        var substations = new List<Substation>
        {
            new Substation { Id = Guid.NewGuid(), SubstationName = "S1" },
            new Substation { Id = Guid.NewGuid(), SubstationName = "S2" }
        };
        _substationRepositoryMock.Setup(s => s.GetSubstationsPagedAsync(1, 10, null, null))
            .ReturnsAsync((substations, 2));

        var handler = new GetSubstationQueryHandler(_substationRepositoryMock.Object);
        var query = new GetSubstaionQuery(1, 10, null, null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Pagination.TotalItems.Should().Be(2);
    }

    #endregion
}
