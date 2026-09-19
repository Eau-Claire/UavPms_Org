using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Features.Reports.Queries.GetReportsPaged;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Reports;

public class GetReportsPagedQueryHandlerTests
{
    private readonly Mock<IReportRepository> _reportRepositoryMock;

    public GetReportsPagedQueryHandlerTests()
    {
        _reportRepositoryMock = new Mock<IReportRepository>();
    }

    [Fact]
    public async Task GetReportsPaged_ShouldNormalizePageAndPageSize_WhenValuesAreOutOfBounds()
    {
        // Arrange
        int capturedPage = 0;
        int capturedPageSize = 0;

        _reportRepositoryMock.Setup(r => r.GetReportsPagedAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<ReportType?>(),
            It.IsAny<ReportStatus?>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>()
        ))
        .Callback<int, int, ReportType?, ReportStatus?, Guid?, Guid?, string?, string?, string?>(
            (p, ps, t, s, tl, sub, q, sb, so) =>
            {
                capturedPage = p;
                capturedPageSize = ps;
            }
        )
        .ReturnsAsync((new List<Report>(), 0));

        var handler = new GetReportsPagedQueryHandler(_reportRepositoryMock.Object);

        // Act - Truyền page = -5 và pageSize = 999
        var query = new GetReportsPagedQuery(-5, 999);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        capturedPage.Should().Be(1);
        capturedPageSize.Should().Be(100);
        result.Pagination.Page.Should().Be(1);
        result.Pagination.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetReportsPaged_ShouldReturnEmptyListAndZeroTotalPages_WhenNoDataMatches()
    {
        // Arrange
        _reportRepositoryMock.Setup(r => r.GetReportsPagedAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<ReportType?>(),
            It.IsAny<ReportStatus?>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>()
        )).ReturnsAsync((new List<Report>(), 0));

        var handler = new GetReportsPagedQueryHandler(_reportRepositoryMock.Object);

        // Act
        var result = await handler.Handle(new GetReportsPagedQuery(1, 10), CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.Pagination.TotalItems.Should().Be(0);
        result.Pagination.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetReportsPaged_ShouldParseFiltersGracefully_WhenValidFilterStringsPassed()
    {
        // Arrange
        ReportType? capturedType = null;
        ReportStatus? capturedStatus = null;

        _reportRepositoryMock.Setup(r => r.GetReportsPagedAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<ReportType?>(),
            It.IsAny<ReportStatus?>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>()
        ))
        .Callback<int, int, ReportType?, ReportStatus?, Guid?, Guid?, string?, string?, string?>(
            (p, ps, t, s, tl, sub, q, sb, so) =>
            {
                capturedType = t;
                capturedStatus = s;
            }
        )
        .ReturnsAsync((new List<Report>(), 0));

        var handler = new GetReportsPagedQueryHandler(_reportRepositoryMock.Object);

        // Act
        var query = new GetReportsPagedQuery(1, 10, Type: "thermal", Status: "approved");
        await handler.Handle(query, CancellationToken.None);

        // Assert
        capturedType.Should().Be(ReportType.Thermal);
        capturedStatus.Should().Be(ReportStatus.Approved);
    }

    [Fact]
    public async Task GetReportsPaged_ShouldIgnoreInvalidFilterStrings_WithoutThrowingException()
    {
        // Arrange
        ReportType? capturedType = null;
        ReportStatus? capturedStatus = null;

        _reportRepositoryMock.Setup(r => r.GetReportsPagedAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<ReportType?>(),
            It.IsAny<ReportStatus?>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>()
        ))
        .Callback<int, int, ReportType?, ReportStatus?, Guid?, Guid?, string?, string?, string?>(
            (p, ps, t, s, tl, sub, q, sb, so) =>
            {
                capturedType = t;
                capturedStatus = s;
            }
        )
        .ReturnsAsync((new List<Report>(), 0));

        var handler = new GetReportsPagedQueryHandler(_reportRepositoryMock.Object);

        // Act - truyền string không phải enum
        var query = new GetReportsPagedQuery(1, 10, Type: "invalid_type", Status: "invalid_status");
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        capturedType.Should().BeNull();
        capturedStatus.Should().BeNull();
        result.Should().NotBeNull();
    }
}
