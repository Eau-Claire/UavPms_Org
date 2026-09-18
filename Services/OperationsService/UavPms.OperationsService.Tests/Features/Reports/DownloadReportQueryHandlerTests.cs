using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Reports.Queries.DownloadReport;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Reports;

public class DownloadReportQueryHandlerTests
{
    private readonly Mock<IReportRepository> _reportRepositoryMock;
    private readonly Mock<IReportExportService> _reportExportServiceMock;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;

    public DownloadReportQueryHandlerTests()
    {
        _reportRepositoryMock = new Mock<IReportRepository>();
        _reportExportServiceMock = new Mock<IReportExportService>();
        _currentUserServicesMock = new Mock<ICurrentUserServices>();
    }

    [Fact]
    public async Task DownloadReport_ShouldReturnFileDownloadDto_WhenPdfFileExists()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Code = "BC-2026-0001",
            Title = "Báo cáo kiểm tra",
            Status = ReportStatus.Approved,
            PdfFileUrl = "/reports/BC-2026-0001.pdf"
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);

        var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake pdf content"));
        _reportExportServiceMock.Setup(s => s.GetReportFileStreamAsync("/reports/BC-2026-0001.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync((stream, "application/pdf", "BC-2026-0001.pdf"));

        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.SystemAdmin });

        var handler = new DownloadReportQueryHandler(
            _reportRepositoryMock.Object,
            _reportExportServiceMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        var result = await handler.Handle(new DownloadReportQuery(reportId, "pdf"), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ContentType.Should().Be("application/pdf");
        result.FileName.Should().Be("BC-2026-0001.pdf");
        result.FileStream.Should().NotBeNull();
    }

    [Fact]
    public async Task DownloadReport_ShouldReturnFileDownloadDto_WhenExcelFileExists()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Code = "BC-2026-0002",
            Title = "Báo cáo kiểm tra",
            Status = ReportStatus.Approved,
            ExcelFileUrl = "/reports/BC-2026-0002.xlsx"
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);

        var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake excel content"));
        _reportExportServiceMock.Setup(s => s.GetReportFileStreamAsync("/reports/BC-2026-0002.xlsx", It.IsAny<CancellationToken>()))
            .ReturnsAsync((stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BC-2026-0002.xlsx"));

        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.SystemAdmin });

        var handler = new DownloadReportQueryHandler(
            _reportRepositoryMock.Object,
            _reportExportServiceMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        var result = await handler.Handle(new DownloadReportQuery(reportId, "excel"), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        result.FileName.Should().Be("BC-2026-0002.xlsx");
        result.FileStream.Should().NotBeNull();
    }

    [Fact]
    public async Task DownloadReport_ShouldThrowNotFoundException_WhenReportDoesNotExist()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync((Report?)null);

        var handler = new DownloadReportQueryHandler(
            _reportRepositoryMock.Object,
            _reportExportServiceMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        Func<Task> act = async () => await handler.Handle(new DownloadReportQuery(reportId, "pdf"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DownloadReport_ShouldThrowNotFoundException_WhenFileHasNotBeenGeneratedYet()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Code = "BC-2026-0003",
            Title = "Báo cáo chưa xuất",
            Status = ReportStatus.Approved,
            PdfFileUrl = null
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.SystemAdmin });

        var handler = new DownloadReportQueryHandler(
            _reportRepositoryMock.Object,
            _reportExportServiceMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        Func<Task> act = async () => await handler.Handle(new DownloadReportQuery(reportId, "pdf"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*chưa được tạo*");
    }

    [Fact]
    public async Task DownloadReport_ShouldThrowForbiddenException_WhenManagerIsOutOfGeographicScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Title = "Báo cáo ngoài vùng",
            Status = ReportStatus.Approved,
            PdfFileUrl = "/reports/file.pdf"
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(false);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new DownloadReportQueryHandler(
            _reportRepositoryMock.Object,
            _reportExportServiceMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        Func<Task> act = async () => await handler.Handle(new DownloadReportQuery(reportId, "pdf"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*ngoài khu vực quản lý*");
    }

    [Fact]
    public async Task DownloadReport_ShouldThrowForbiddenException_WhenInspectorDownloadsAnotherUsersDraftReport()
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
            CreatedBy = creatorId,
            PdfFileUrl = "/reports/draft.pdf"
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(inspectorId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new DownloadReportQueryHandler(
            _reportRepositoryMock.Object,
            _reportExportServiceMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        Func<Task> act = async () => await handler.Handle(new DownloadReportQuery(reportId, "pdf"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*chỉ có thể tải báo cáo do chính mình tạo ra*");
    }
}
