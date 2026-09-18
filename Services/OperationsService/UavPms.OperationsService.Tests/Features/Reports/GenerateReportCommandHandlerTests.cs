using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Reports.Commands.GenerateReport;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Reports;

public class GenerateReportCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IReportRepository> _reportRepositoryMock;
    private readonly Mock<IReportExportService> _reportExportServiceMock;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;

    public GenerateReportCommandHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _reportRepositoryMock = new Mock<IReportRepository>();
        _reportExportServiceMock = new Mock<IReportExportService>();
        _currentUserServicesMock = new Mock<ICurrentUserServices>();
    }

    [Fact]
    public async Task GenerateReport_ShouldGeneratePdfAndUpdateReport_WhenFormatIsPdf()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Code = "BC-2026-0010",
            Title = "Báo cáo định kỳ 500kV",
            Type = ReportType.Periodic,
            Status = ReportStatus.Draft,
            CreatedBy = creatorId
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.GetAnomaliesByMissionIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new List<DetectedAnomaly>());

        byte[] fakePdf = new byte[] { 1, 2, 3, 4, 5 };
        _reportExportServiceMock.Setup(s => s.GenerateReportFileAsync(report, It.IsAny<IReadOnlyList<DetectedAnomaly>>(), "pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync((fakePdf, "application/pdf", "BC-2026-0010.pdf"));

        _reportExportServiceMock.Setup(s => s.SaveReportFileAsync(It.IsAny<Stream>(), "BC-2026-0010.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync("/reports/BC-2026-0010.pdf");

        _currentUserServicesMock.Setup(c => c.UserId).Returns(creatorId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new GenerateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _reportExportServiceMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        var result = await handler.Handle(new GenerateReportCommand(reportId, "pdf"), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        report.PdfFileUrl.Should().Be("/reports/BC-2026-0010.pdf");
        report.PdfFileSize.Should().Be(5);
        _reportRepositoryMock.Verify(r => r.UpdateAsync(report), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateReport_ShouldGenerateExcelAndUpdateReport_WhenFormatIsExcel()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Code = "BC-2026-0011",
            Title = "Báo cáo khuyết tật",
            Type = ReportType.Defect,
            Status = ReportStatus.Draft,
            CreatedBy = creatorId
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.GetAnomaliesByMissionIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new List<DetectedAnomaly>());

        byte[] fakeExcel = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };
        _reportExportServiceMock.Setup(s => s.GenerateReportFileAsync(report, It.IsAny<IReadOnlyList<DetectedAnomaly>>(), "excel", It.IsAny<CancellationToken>()))
            .ReturnsAsync((fakeExcel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BC-2026-0011.xlsx"));

        _reportExportServiceMock.Setup(s => s.SaveReportFileAsync(It.IsAny<Stream>(), "BC-2026-0011.xlsx", It.IsAny<CancellationToken>()))
            .ReturnsAsync("/reports/BC-2026-0011.xlsx");

        _currentUserServicesMock.Setup(c => c.UserId).Returns(creatorId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new GenerateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _reportExportServiceMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        var result = await handler.Handle(new GenerateReportCommand(reportId, "excel"), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        report.ExcelFileUrl.Should().Be("/reports/BC-2026-0011.xlsx");
        report.ExcelFileSize.Should().Be(8);
        _reportRepositoryMock.Verify(r => r.UpdateAsync(report), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateReport_ShouldThrowNotFoundException_WhenReportDoesNotExist()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync((Report?)null);

        var handler = new GenerateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _reportExportServiceMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        Func<Task> act = async () => await handler.Handle(new GenerateReportCommand(reportId, "pdf"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GenerateReport_ShouldThrowForbiddenException_WhenManagerIsOutOfGeographicScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = ReportStatus.Draft };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(false);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new GenerateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _reportExportServiceMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        Func<Task> act = async () => await handler.Handle(new GenerateReportCommand(reportId, "pdf"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*ngoài khu vực quản lý*");
    }

    [Fact]
    public async Task GenerateReport_ShouldThrowForbiddenException_WhenInspectorGeneratesAnotherUsersDraftReport()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = ReportStatus.Draft, CreatedBy = creatorId };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(otherUserId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new GenerateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _reportExportServiceMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        Func<Task> act = async () => await handler.Handle(new GenerateReportCommand(reportId, "pdf"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*chỉ có thể xuất bản báo cáo do chính mình tạo ra*");
    }
}
