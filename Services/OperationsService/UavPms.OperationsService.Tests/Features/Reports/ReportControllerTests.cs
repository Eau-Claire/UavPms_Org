using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Application.Features.Reports.Commands.ApproveReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.CreateReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.DeleteReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.GenerateReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.RejectReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.SubmitReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.UpdateReport;
using UavPms.OperationsService.Application.Features.Reports.DTOs;
using UavPms.OperationsService.Application.Features.Reports.Queries.DownloadReport;
using UavPms.OperationsService.Application.Features.Reports.Queries.GetReportById;
using UavPms.OperationsService.Application.Features.Reports.Queries.GetReportsPaged;
using UavPms.OperationsService.Application.Features.Reports.Queries.GetReportStatistics;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Reports;

public class ReportControllerTests
{
    private readonly Mock<ISender> _mediatorMock;
    private readonly ReportController _controller;

    public ReportControllerTests()
    {
        _mediatorMock = new Mock<ISender>();
        _controller = new ReportController(_mediatorMock.Object);
    }

    [Fact]
    public async Task GetPaged_ShouldReturnOkWithApiResponse()
    {
        // Arrange
        var pagedResponse = new PaginatedReportsResponse(
            new List<ReportDto>(),
            new PaginationMetaData(1, 10, 0, 0)
        );
        _mediatorMock.Setup(m => m.Send(It.IsAny<GetReportsPagedQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResponse);

        // Act
        var result = await _controller.GetPaged(1, 10, "defect", "draft");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().Be(pagedResponse);
    }

    [Fact]
    public async Task GetStatistics_ShouldReturnOkWithApiResponse()
    {
        // Arrange
        var stats = new ReportStatisticsDto(
            5,
            new Dictionary<string, int> { ["defect"] = 2, ["periodic"] = 1, ["thermal"] = 1, ["corridor"] = 1 },
            new Dictionary<string, int> { ["draft"] = 2, ["pending"] = 2, ["approved"] = 1 }
        );
        _mediatorMock.Setup(m => m.Send(It.IsAny<GetReportStatisticsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        // Act
        var result = await _controller.GetStatistics();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().Be(stats);
    }

    [Fact]
    public async Task GetById_ShouldReturnOkWithApiResponse()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var detailDto = CreateFakeReportDetail(reportId, "BC-2026-0001", "Báo cáo kiểm tra", "draft");
        _mediatorMock.Setup(m => m.Send(It.Is<GetReportByIdQuery>(q => q.Id == reportId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailDto);

        // Act
        var result = await _controller.GetById(reportId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().Be(detailDto);
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtActionWith201AndApiResponse()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var detailDto = CreateFakeReportDetail(reportId, "BC-2026-0002", "Báo cáo mới", "draft");
        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateReportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailDto);

        var request = new CreateReportRequest("Báo cáo mới", "defect");

        // Act
        var result = await _controller.Create(request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(ReportController.GetById));
        createdResult.RouteValues!["id"].Should().Be(reportId);

        var apiResponse = createdResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().Be(detailDto);
    }

    [Fact]
    public async Task Update_ShouldReturnOkWithApiResponse()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var detailDto = CreateFakeReportDetail(reportId, "BC-2026-0003", "Tiêu đề đã sửa", "draft");
        _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateReportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailDto);

        var request = new UpdateReportRequest("Tiêu đề đã sửa");

        // Act
        var result = await _controller.Update(reportId, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().Be(detailDto);
    }

    [Fact]
    public async Task Delete_ShouldReturnOkWithApiResponse()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        _mediatorMock.Setup(m => m.Send(It.Is<DeleteReportCommand>(c => c.Id == reportId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(reportId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Submit_ShouldReturnOkWithApiResponse()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var detailDto = CreateFakeReportDetail(reportId, "BC-2026-0004", "Báo cáo gửi duyệt", "pending");
        _mediatorMock.Setup(m => m.Send(It.Is<SubmitReportCommand>(c => c.Id == reportId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailDto);

        // Act
        var result = await _controller.Submit(reportId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().Be(detailDto);
    }

    [Fact]
    public async Task Approve_ShouldReturnOkWithApiResponse()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var detailDto = CreateFakeReportDetail(reportId, "BC-2026-0005", "Báo cáo đã duyệt", "approved");
        _mediatorMock.Setup(m => m.Send(It.Is<ApproveReportCommand>(c => c.Id == reportId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailDto);

        // Act
        var result = await _controller.Approve(reportId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().Be(detailDto);
    }

    [Fact]
    public async Task Reject_ShouldReturnOkWithApiResponse()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var detailDto = CreateFakeReportDetail(reportId, "BC-2026-0006", "Báo cáo bị từ chối", "draft");
        _mediatorMock.Setup(m => m.Send(It.Is<RejectReportCommand>(c => c.Id == reportId && c.Reason == "Chưa đủ dữ liệu"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailDto);

        var request = new RejectReportRequest("Chưa đủ dữ liệu");

        // Act
        var result = await _controller.Reject(reportId, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().Be(detailDto);
    }

    [Fact]
    public async Task Generate_ShouldReturnOkWithApiResponse()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var detailDto = CreateFakeReportDetail(reportId, "BC-2026-0007", "Báo cáo xuất bản", "approved");
        _mediatorMock.Setup(m => m.Send(It.Is<GenerateReportCommand>(c => c.Id == reportId && c.Format == "pdf"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailDto);

        // Act
        var result = await _controller.Generate(reportId, "pdf");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().Be(detailDto);
    }

    [Fact]
    public async Task Download_ShouldReturnFileStreamResultWithCorrectContentTypeAndFileName()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample pdf content"));
        var downloadDto = new ReportFileDownloadDto(stream, "application/pdf", "BC-2026-0008.pdf");
        _mediatorMock.Setup(m => m.Send(It.Is<DownloadReportQuery>(q => q.Id == reportId && q.Format == "pdf"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadDto);

        // Act
        var result = await _controller.Download(reportId, "pdf");

        // Assert
        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be("BC-2026-0008.pdf");
        fileResult.FileStream.Should().BeSameAs(stream);
    }

    private static ReportDetailDto CreateFakeReportDetail(Guid id, string code, string title, string status)
    {
        return new ReportDetailDto(
            id,
            code,
            title,
            "periodic",
            status,
            "Mô tả",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            0,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            new List<ReportMissionSummaryDto>()
        );
    }
}
