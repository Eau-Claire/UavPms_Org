using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Reports.Queries.GetReportById;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Reports;

public class GetReportByIdQueryHandlerTests
{
    private readonly Mock<IReportRepository> _reportRepositoryMock;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;

    public GetReportByIdQueryHandlerTests()
    {
        _reportRepositoryMock = new Mock<IReportRepository>();
        _currentUserServicesMock = new Mock<ICurrentUserServices>();
    }

    [Fact]
    public async Task GetReportById_ShouldReturnDetailDto_WhenReportExists()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Code = "BC-2026-0001",
            Title = "Báo cáo kiểm tra",
            Type = ReportType.Periodic,
            Status = ReportStatus.Approved
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);

        var handler = new GetReportByIdQueryHandler(_reportRepositoryMock.Object);

        // Act
        var result = await handler.Handle(new GetReportByIdQuery(reportId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(reportId);
        result.Code.Should().Be("BC-2026-0001");
        result.Title.Should().Be("Báo cáo kiểm tra");
    }

    [Fact]
    public async Task GetReportById_ShouldThrowNotFoundException_WhenReportDoesNotExist()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync((Report?)null);

        var handler = new GetReportByIdQueryHandler(_reportRepositoryMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new GetReportByIdQuery(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetReportById_ShouldThrowForbiddenException_WhenManagerIsOutOfGeographicScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Title = "Báo cáo ngoài vùng",
            Status = ReportStatus.Pending
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(false);
        _currentUserServicesMock.Setup(c => c.IsAuthenticated).Returns(true);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new GetReportByIdQueryHandler(_reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new GetReportByIdQuery(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*ngoài khu vực quản lý*");
    }

    [Fact]
    public async Task GetReportById_ShouldReturnDetailDto_WhenManagerIsInGeographicScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Code = "BC-2026-0002",
            Title = "Báo cáo trong vùng",
            Type = ReportType.Defect,
            Status = ReportStatus.Pending
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(true);
        _currentUserServicesMock.Setup(c => c.IsAuthenticated).Returns(true);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new GetReportByIdQueryHandler(_reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        var result = await handler.Handle(new GetReportByIdQuery(reportId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Code.Should().Be("BC-2026-0002");
    }

    [Fact]
    public async Task GetReportById_ShouldThrowForbiddenException_WhenInspectorViewsOtherUsersDraftReport()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Title = "Báo cáo nháp của người khác",
            Status = ReportStatus.Draft,
            CreatedBy = creatorId
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.IsAuthenticated).Returns(true);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(inspectorId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new GetReportByIdQueryHandler(_reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new GetReportByIdQuery(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*chỉ có thể xem báo cáo nháp do chính mình tạo ra*");
    }

    [Fact]
    public async Task GetReportById_ShouldReturnDetailDto_WhenInspectorViewsTheirOwnDraftReport()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Code = "BC-2026-0003",
            Title = "Báo cáo nháp của chính mình",
            Type = ReportType.Periodic,
            Status = ReportStatus.Draft,
            CreatedBy = inspectorId
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.IsAuthenticated).Returns(true);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(inspectorId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new GetReportByIdQueryHandler(_reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        var result = await handler.Handle(new GetReportByIdQuery(reportId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Báo cáo nháp của chính mình");
    }
}
