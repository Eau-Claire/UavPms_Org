using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.Application.Features.AuditLogs.DTOs;
using UavPms.Application.Features.AuditLogs.Queries;
using UavPms.Application.Features.AuditLogs.Queries.GetAuditLogs;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using Xunit;

namespace UavPms.UnitTests.Features.AuditLogs;

public class GetAuditLogsQueryHandlerTests
{
    private readonly Mock<IAuditLogRepository> _repositoryMock;
    private readonly GetAuditLogsQueryHandler _handler;

    public GetAuditLogsQueryHandlerTests()
    {
        _repositoryMock = new Mock<IAuditLogRepository>();
        _handler = new GetAuditLogsQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnPaginatedAuditLogs_WhenCalled()
    {
        var query = new GetAuditLogsQuery(1, 10, null, null, null);
        var mockLogs = new List<AuditLog>
        {
            new() { TableName = "Users", ActionType = "Added", RecordId = Guid.NewGuid() },
            new() { TableName = "Assets", ActionType = "Modified", RecordId = Guid.NewGuid() }
        };

        _repositoryMock.Setup(r => r.GetAuditLogsPagedAsync(1, 10, null, null, null))
            .ReturnsAsync((mockLogs, 2));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Pagination.TotalItems.Should().Be(2);
        result.Pagination.TotalPages.Should().Be(1);
    }
}
