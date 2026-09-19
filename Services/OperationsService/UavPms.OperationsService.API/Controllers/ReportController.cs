using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Reports.Commands.ApproveReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.CreateReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.DeleteReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.GenerateReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.RejectReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.SubmitReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.UpdateReport;
using UavPms.OperationsService.Application.Features.Reports.Queries.DownloadReport;
using UavPms.OperationsService.Application.Features.Reports.Queries.GetReportById;
using UavPms.OperationsService.Application.Features.Reports.Queries.GetReportsPaged;
using UavPms.OperationsService.Application.Features.Reports.Queries.GetReportStatistics;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/reports")]
[ApiVersion("1.0")]
[Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
public class ReportController : ControllerBase
{
    private readonly ISender _mediator;

    public ReportController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy danh sách báo cáo có hỗ trợ phân trang, tìm kiếm và bộ lọc server-side.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? transmissionLineId = null,
        [FromQuery] Guid? substationId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null)
    {
        var query = new GetReportsPagedQuery(
            page,
            pageSize,
            type,
            status,
            transmissionLineId,
            substationId,
            search,
            sortBy,
            sortOrder
        );
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy danh sách báo cáo thành công.", result));
    }

    /// <summary>
    /// Lấy số liệu thống kê tổng hợp báo cáo theo loại và trạng thái.
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? period = null)
    {
        var query = new GetReportStatisticsQuery(from, to, period);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy thống kê báo cáo thành công.", result));
    }

    /// <summary>
    /// Lấy thông tin chi tiết một báo cáo theo ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetReportByIdQuery(id);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy thông tin báo cáo thành công.", result));
    }

    /// <summary>
    /// Tạo mới báo cáo quản trị (trạng thái mặc định: draft).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.AdminManagerInspectorAnalyst)]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest request)
    {
        var command = new CreateReportCommand(
            request.Title,
            request.Type,
            request.TransmissionLineId,
            request.SubstationId,
            request.MissionIds,
            request.Description,
            request.DateFrom,
            request.DateTo
        );
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse(true, "Tạo báo cáo thành công.", result));
    }

    /// <summary>
    /// Cập nhật thông tin báo cáo (chỉ cho phép khi ở trạng thái draft).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminManagerInspectorAnalyst)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReportRequest request)
    {
        var command = new UpdateReportCommand(
            id,
            request.Title,
            request.Description,
            request.TransmissionLineId,
            request.SubstationId,
            request.MissionIds,
            request.DateFrom,
            request.DateTo
        );
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Cập nhật báo cáo thành công.", result));
    }

    /// <summary>
    /// Xóa mềm báo cáo (chỉ cho phép khi ở trạng thái draft).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminManagerInspectorAnalyst)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var command = new DeleteReportCommand(id);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Xóa báo cáo thành công."));
    }

    /// <summary>
    /// Gửi báo cáo lên cấp quản lý duyệt (chuyển draft -> pending).
    /// </summary>
    [HttpPut("{id:guid}/submit")]
    [Authorize(Roles = UserRoles.AdminManagerInspectorAnalyst)]
    public async Task<IActionResult> Submit(Guid id)
    {
        var command = new SubmitReportCommand(id);
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Gửi duyệt báo cáo thành công.", result));
    }

    /// <summary>
    /// Phê duyệt báo cáo (chuyển pending -> approved).
    /// </summary>
    [HttpPut("{id:guid}/approve")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Approve(Guid id)
    {
        var command = new ApproveReportCommand(id);
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Phê duyệt báo cáo thành công.", result));
    }

    /// <summary>
    /// Từ chối báo cáo kèm lý do (chuyển pending -> draft).
    /// </summary>
    [HttpPut("{id:guid}/reject")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectReportRequest request)
    {
        var command = new RejectReportCommand(id, request.Reason);
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Từ chối duyệt báo cáo thành công.", result));
    }

    /// <summary>
    /// Kích hoạt tạo file xuất bản PDF hoặc Excel cho báo cáo.
    /// </summary>
    [HttpPost("{id:guid}/generate")]
    [Authorize(Roles = UserRoles.AdminManagerInspectorAnalyst)]
    public async Task<IActionResult> Generate(Guid id, [FromQuery] string format = "pdf")
    {
        var command = new GenerateReportCommand(id, format);
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, $"Tạo tệp báo cáo định dạng {format.ToUpperInvariant()} thành công.", result));
    }

    /// <summary>
    /// Tải tệp báo cáo đã xuất bản.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [Authorize(Roles = UserRoles.AdminManagerInspectorAnalyst)]
    public async Task<IActionResult> Download(Guid id, [FromQuery] string format = "pdf")
    {
        var query = new DownloadReportQuery(id, format);
        var result = await _mediator.Send(query);
        return File(result.FileStream, result.ContentType, result.FileName);
    }
}

// Các Request DTOs được khai báo ở cuối Controller theo quy chuẩn conventions.md
public record CreateReportRequest(
    string Title,
    string Type,
    Guid? TransmissionLineId = null,
    Guid? SubstationId = null,
    List<Guid>? MissionIds = null,
    string? Description = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null
);

public record UpdateReportRequest(
    string Title,
    string? Description = null,
    Guid? TransmissionLineId = null,
    Guid? SubstationId = null,
    List<Guid>? MissionIds = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null
);

public record RejectReportRequest(
    string Reason
);
