using System;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Maintenance;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/maintenance/tickets")]
[Authorize]
public class MaintenanceTicketController : ControllerBase
{
    private readonly IMaintenanceTicketService _ticketService;

    public MaintenanceTicketController(IMaintenanceTicketService ticketService)
    {
        _ticketService = ticketService;
    }

    /// <summary>
    /// Tạo phiếu bảo trì mới (Chỉ dành cho Manager hoặc Admin)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> CreateTicket([FromBody] CreateMaintenanceTicketRequest request, CancellationToken ct)
    {
        var result = await _ticketService.CreateTicketAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse(true, "Maintenance ticket created successfully.", result));
    }

    /// <summary>
    /// Cập nhật trạng thái phiếu bảo trì
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> UpdateStatus([FromRoute] Guid id, [FromBody] UpdateTicketStatusRequest request, CancellationToken ct)
    {
        var result = await _ticketService.UpdateTicketStatusAsync(id, request.Status, ct);
        return Ok(new ApiResponse(true, "Maintenance ticket status updated successfully.", result));
    }

    /// <summary>
    /// Upload ảnh minh chứng sau sửa chữa và ghi chú kỹ thuật viên
    /// </summary>
    [HttpPost("{id:guid}/proof")]
    [Authorize(Roles = $"{UserRoles.Technician},{UserRoles.MaintenanceTechnician},{UserRoles.AdminAndManager}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadProof(
        [FromRoute] Guid id,
        // No [FromForm] on IFormFile: Swashbuckle rejects it and the whole swagger.json fails (500).
        IFormFile file,
        [FromForm] string? technicianNotes,
        CancellationToken ct)
    {
        var result = await _ticketService.UploadProofAsync(id, file, technicianNotes, ct);
        return Ok(new ApiResponse(true, "Repair proof photo uploaded successfully.", result));
    }

    /// <summary>
    /// Khai báo nhật ký vật tư thiết bị đã thay thế
    /// </summary>
    [HttpPost("{id:guid}/materials")]
    [Authorize(Roles = $"{UserRoles.Technician},{UserRoles.MaintenanceTechnician},{UserRoles.AdminAndManager}")]
    public async Task<IActionResult> LogMaterial([FromRoute] Guid id, [FromBody] LogMaterialRequest request, CancellationToken ct)
    {
        var result = await _ticketService.LogMaterialAsync(id, request, ct);
        return Ok(new ApiResponse(true, "Engineering material logged successfully.", result));
    }

    /// <summary>
    /// Nghiệm thu và đóng phiếu bảo trì (Chỉ dành cho Manager hoặc Admin)
    /// </summary>
    [HttpPut("{id:guid}/close")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> CloseTicket([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _ticketService.CloseTicketAsync(id, ct);
        return Ok(new ApiResponse(true, "Maintenance ticket closed successfully.", result));
    }

    /// <summary>
    /// Lấy danh sách phiếu bảo trì được phân công cho kỹ thuật viên đang đăng nhập
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = $"{UserRoles.Technician},{UserRoles.MaintenanceTechnician}")]
    public async Task<IActionResult> GetMyTickets(CancellationToken ct)
    {
        var result = await _ticketService.GetMyTicketsAsync(ct);
        return Ok(new ApiResponse(true, "Assigned maintenance tickets retrieved successfully.", result));
    }

    /// <summary>
    /// Lấy danh sách toàn bộ phiếu bảo trì (có phân trang và bộ lọc)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> GetTickets(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        CancellationToken ct = default)
    {
        var result = await _ticketService.GetTicketsAsync(pageIndex, pageSize, status, priority, ct);
        return Ok(new ApiResponse(true, "Maintenance tickets retrieved successfully.", result));
    }

    /// <summary>
    /// Lấy chi tiết phiếu bảo trì theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> GetTicketById([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _ticketService.GetTicketByIdAsync(id, ct);
        return Ok(new ApiResponse(true, "Maintenance ticket retrieved successfully.", result));
    }
}
