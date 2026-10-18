using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Alerts;
using UavPms.OperationsService.Application.Features.Alerts.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/alerts")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly IEmergencyAlertService _alertService;
    private readonly ICurrentUserServices _currentUserServices;

    public AlertsController(
        IEmergencyAlertService alertService,
        ICurrentUserServices currentUserServices)
    {
        _alertService = alertService;
        _currentUserServices = currentUserServices;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ cảnh báo khẩn cấp đang active
    /// </summary>
    [HttpGet("active")]
    [Authorize(Roles = UserRoles.AdminManagerAnalyst)]
    [ProducesResponseType(typeof(List<ActiveEmergencyAlertDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveAlerts(CancellationToken ct)
    {
        var alerts = await _alertService.GetActiveAlertsAsync(ct);
        return Ok(alerts);
    }

    /// <summary>
    /// Lấy danh sách lịch sử cảnh báo (có phân trang và lọc trạng thái)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = UserRoles.AdminManagerAnalyst)]
    [ProducesResponseType(typeof(PaginatedAlertsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _alertService.GetAlertHistoryAsync(status, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một cảnh báo khẩn cấp (kèm lịch sử chuyển cấp)
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminManagerAnalyst)]
    [ProducesResponseType(typeof(EmergencyAlertDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAlertById([FromRoute] Guid id, CancellationToken ct)
    {
        var alert = await _alertService.GetAlertByIdAsync(id, ct);
        if (alert == null)
        {
            return NotFound(new { message = $"Emergency alert with ID {id} was not found." });
        }

        return Ok(alert);
    }

    /// <summary>
    /// Thẩm định cảnh báo khẩn cấp (Analyst, Manager, Admin) - Xác nhận hoặc Bác bỏ cảnh báo
    /// </summary>
    [HttpPut("{id:guid}/review")]
    [Authorize(Roles = UserRoles.AdminManagerAnalyst)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewAlert(
        [FromRoute] Guid id,
        [FromBody] ReviewEmergencyAlertRequest request,
        CancellationToken ct)
    {
        var reviewerId = _currentUserServices.UserId;
        var success = await _alertService.ReviewAlertAsync(id, reviewerId, request, ct);
        if (!success)
        {
            return NotFound(new { message = $"Emergency alert with ID {id} was not found." });
        }

        return NoContent();
    }

    /// <summary>
    /// Chuyển cấp cảnh báo khẩn cấp lên Manager chỉ định (Analyst, Admin)
    /// </summary>
    [HttpPost("{id:guid}/escalate")]
    [Authorize(Roles = UserRoles.AdminManagerAnalyst)]
    [ProducesResponseType(typeof(AlertEscalationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EscalateAlert(
        [FromRoute] Guid id,
        [FromBody] EscalateEmergencyAlertRequest request,
        CancellationToken ct)
    {
        try
        {
            var currentUserId = _currentUserServices.UserId;
            var escalation = await _alertService.EscalateAlertAsync(id, currentUserId, request, ct);
            return StatusCode(StatusCodes.Status201Created, escalation);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Kích hoạt cảnh báo khẩn cấp từ Edge AI / Drone / Simulator
    /// </summary>
    [HttpPost("trigger")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    [ProducesResponseType(typeof(EmergencyAlertDetailDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> TriggerAlert(
        [FromBody] TriggerEmergencyAlertRequest request,
        CancellationToken ct)
    {
        var result = await _alertService.TriggerAlertAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
