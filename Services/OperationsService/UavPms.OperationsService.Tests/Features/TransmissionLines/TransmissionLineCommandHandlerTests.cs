using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.TransmissionLines.Commands.CreateTransmissionLine;
using UavPms.OperationsService.Application.Features.TransmissionLines.Commands.DeleteTransmissionLine;
using UavPms.OperationsService.Application.Features.TransmissionLines.Commands.UpdateTransmissionLine;
using UavPms.OperationsService.Application.Features.TransmissionLines.Queries.GetTransmissionLines;
using UavPms.OperationsService.Application.Features.TransmissionLines.Queries.GetTransmissionLinesById;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.TransmissionLines;

public class TransmissionLineCommandHandlerTests
{
    private readonly Mock<ITransmissionLineRepository> _transmissionLineRepositoryMock;
    private readonly Mock<ISubstationRepository> _substationRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public TransmissionLineCommandHandlerTests()
    {
        _transmissionLineRepositoryMock = new Mock<ITransmissionLineRepository>();
        _substationRepositoryMock = new Mock<ISubstationRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }

    #region CreateTransmissionLineCommandHandler Tests

    [Fact]
    public async Task CreateTransmissionLine_ShouldCreateLine_WhenSubstationExists()
    {
        // Arrange
        var substationId = Guid.NewGuid();
        var substation = new Substation { Id = substationId, SubstationName = "Substation A", IsDeleted = false };
        _substationRepositoryMock.Setup(s => s.GetByIdAsync(substationId, true)).ReturnsAsync(substation);

        var handler = new CreateTransmissionLineCommandHandler(
            _unitOfWorkMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object);

        var command = new CreateTransmissionLineCommand(substationId, "Line 500kV Hanoi-Haiphong", true, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.LineName.Should().Be("Line 500kV Hanoi-Haiphong");
        _transmissionLineRepositoryMock.Verify(l => l.AddAsync(It.Is<TransmissionLine>(x => x.LineName == "Line 500kV Hanoi-Haiphong")), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTransmissionLine_ShouldThrowNotFoundException_WhenSubstationDoesNotExist()
    {
        // Arrange
        var substationId = Guid.NewGuid();
        _substationRepositoryMock.Setup(s => s.GetByIdAsync(substationId, true)).ReturnsAsync((Substation?)null);

        var handler = new CreateTransmissionLineCommandHandler(
            _unitOfWorkMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object);

        var command = new CreateTransmissionLineCommand(substationId, "Line X", false, null);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region UpdateTransmissionLineCommandHandler Tests

    [Fact]
    public async Task UpdateTransmissionLine_ShouldUpdateLine_WhenLineAndSubstationExist()
    {
        // Arrange
        var lineId = Guid.NewGuid();
        var substationId = Guid.NewGuid();
        var existingLine = new TransmissionLine { Id = lineId, SubstationAssetId = substationId, LineName = "Old Line Name", IsDeleted = false };
        var substation = new Substation { Id = substationId, SubstationName = "Substation A", IsDeleted = false };

        _transmissionLineRepositoryMock.Setup(l => l.GetByIdAsync(lineId, true)).ReturnsAsync(existingLine);
        _substationRepositoryMock.Setup(s => s.GetByIdAsync(substationId, true)).ReturnsAsync(substation);

        var handler = new UpdateTransmissionLineCommandHandler(
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new UpdateTransmissionLineCommand(lineId, substationId, "Updated Line Name", true, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.LineName.Should().Be("Updated Line Name");
        _transmissionLineRepositoryMock.Verify(l => l.UpdateAsync(existingLine), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTransmissionLine_ShouldThrowNotFoundException_WhenLineDoesNotExist()
    {
        // Arrange
        var lineId = Guid.NewGuid();
        _transmissionLineRepositoryMock.Setup(l => l.GetByIdAsync(lineId, true)).ReturnsAsync((TransmissionLine?)null);

        var handler = new UpdateTransmissionLineCommandHandler(
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _unitOfWorkMock.Object);

        var command = new UpdateTransmissionLineCommand(lineId, Guid.NewGuid(), "Line Y", false, null);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region DeleteTransmissionLineCommandHandler Tests

    [Fact]
    public async Task DeleteTransmissionLine_ShouldDeleteLine_WhenExists()
    {
        // Arrange
        var lineId = Guid.NewGuid();
        var line = new TransmissionLine { Id = lineId, LineName = "Line to Delete", IsDeleted = false };
        _transmissionLineRepositoryMock.Setup(l => l.GetByIdAsync(lineId, true)).ReturnsAsync(line);

        var handler = new DeleteTransmissionLineCommandHandler(_transmissionLineRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new DeleteTransmissionLineCommand(lineId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _transmissionLineRepositoryMock.Verify(l => l.DeleteAsync(line), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Query Handler Tests

    [Fact]
    public async Task GetTransmissionLineById_ShouldReturnLineDto_WhenExists()
    {
        // Arrange
        var lineId = Guid.NewGuid();
        var line = new TransmissionLine { Id = lineId, LineName = "Line 500kV", IsDeleted = false };
        _transmissionLineRepositoryMock.Setup(l => l.GetByIdAsync(lineId, true)).ReturnsAsync(line);

        var handler = new GetTransmissionLineByIdQueryHandler(_transmissionLineRepositoryMock.Object);
        var query = new GetTransmissionLineByIdQuery(lineId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(lineId);
        result.LineName.Should().Be("Line 500kV");
    }

    [Fact]
    public async Task GetTransmissionLines_ShouldReturnPaginatedList_WhenCalled()
    {
        // Arrange
        var lines = new List<TransmissionLine>
        {
            new TransmissionLine { Id = Guid.NewGuid(), LineName = "L1" },
            new TransmissionLine { Id = Guid.NewGuid(), LineName = "L2" }
        };
        _transmissionLineRepositoryMock.Setup(l => l.GetTransmissionLinesPagedAsync(1, 10, null, null))
            .ReturnsAsync((lines, 2));

        var handler = new GetTransmissionLinesQueryHandler(_transmissionLineRepositoryMock.Object);
        var query = new GetTransmissionLinesQuery(1, 10, null, null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Pagination.TotalItems.Should().Be(2);
    }

    #endregion
}
