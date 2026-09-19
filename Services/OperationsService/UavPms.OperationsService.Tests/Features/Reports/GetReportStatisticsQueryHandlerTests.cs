using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Features.Reports.Queries.GetReportStatistics;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Reports;

public class GetReportStatisticsQueryHandlerTests
{
    private readonly Mock<IReportRepository> _reportRepositoryMock;

    public GetReportStatisticsQueryHandlerTests()
    {
        _reportRepositoryMock = new Mock<IReportRepository>();
    }

    [Fact]
    public async Task GetReportStatistics_ShouldReturnStatistics_WhenDataAvailable()
    {
        // Arrange
        var byType = new Dictionary<string, int>
        {
            ["defect"] = 10,
            ["periodic"] = 5,
            ["thermal"] = 3,
            ["corridor"] = 2
        };
        var byStatus = new Dictionary<string, int>
        {
            ["approved"] = 12,
            ["pending"] = 5,
            ["draft"] = 3
        };

        _reportRepositoryMock.Setup(r => r.GetReportStatisticsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((20, byType, byStatus));

        var handler = new GetReportStatisticsQueryHandler(_reportRepositoryMock.Object);

        // Act
        var result = await handler.Handle(new GetReportStatisticsQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(20);
        result.ByType["defect"].Should().Be(10);
        result.ByStatus["approved"].Should().Be(12);
    }

    [Theory]
    [InlineData("month")]
    [InlineData("quarter")]
    [InlineData("year")]
    public async Task GetReportStatistics_ShouldComputeDateRange_WhenPeriodProvided(string period)
    {
        // Arrange
        DateTimeOffset? capturedFrom = null;
        DateTimeOffset? capturedTo = null;

        _reportRepositoryMock.Setup(r => r.GetReportStatisticsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>()))
            .Callback<DateTimeOffset?, DateTimeOffset?>((f, t) =>
            {
                capturedFrom = f;
                capturedTo = t;
            })
            .ReturnsAsync((0, new Dictionary<string, int>(), new Dictionary<string, int>()));

        var handler = new GetReportStatisticsQueryHandler(_reportRepositoryMock.Object);

        // Act
        await handler.Handle(new GetReportStatisticsQuery(Period: period), CancellationToken.None);

        // Assert
        capturedFrom.Should().NotBeNull();
        capturedTo.Should().NotBeNull();
        capturedFrom.Should().BeBefore(capturedTo!.Value);
    }

    [Fact]
    public async Task GetReportStatistics_ShouldReturnZeroes_WhenNoReportsExist()
    {
        // Arrange
        var emptyByType = new Dictionary<string, int>
        {
            ["defect"] = 0,
            ["periodic"] = 0,
            ["thermal"] = 0,
            ["corridor"] = 0
        };
        var emptyByStatus = new Dictionary<string, int>
        {
            ["approved"] = 0,
            ["pending"] = 0,
            ["draft"] = 0
        };

        _reportRepositoryMock.Setup(r => r.GetReportStatisticsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync((0, emptyByType, emptyByStatus));

        var handler = new GetReportStatisticsQueryHandler(_reportRepositoryMock.Object);

        // Act
        var result = await handler.Handle(new GetReportStatisticsQuery(), CancellationToken.None);

        // Assert
        result.Total.Should().Be(0);
        result.ByType["defect"].Should().Be(0);
        result.ByStatus["draft"].Should().Be(0);
    }
}
