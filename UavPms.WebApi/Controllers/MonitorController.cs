using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UavPms.Application.Features.Monitor.Queries.GetActiveAlerts;
using UavPms.Application.Features.Monitor.Queries.GetDashboardSummary;
using UavPms.Application.Features.Monitor.Queries.GetDefectStatistics;
using UavPms.Application.Features.Monitor.Queries.GetInspectionHistory;
using UavPms.Application.Features.Monitor.Queries.GetMissionStatusOverview;
using UavPms.Application.Features.Monitor.Queries.GetRecentDefects;
using Microsoft.AspNetCore.Authorization;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/monitor")]
[ApiVersion("1.0")]
[Authorize]
public class MonitorController : ControllerBase
{
    private readonly ISender _mediator;

    public MonitorController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("summary")]
    [Authorize(Roles = "SystemAdmin, Manager, Analyst")]
    public async Task<IActionResult> GetSummary()
    {
        var result = await _mediator.Send(new GetDashboardSummaryQuery());
        return Ok(new ApiResponse(true, "Dashboard summary retrieved successfully", result));
    }

    [HttpGet("recent-defects")]
    [Authorize(Roles = "SystemAdmin, Manager, Analyst, Technician")]
    public async Task<IActionResult> GetRecentDefects([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if(page <= 0 || pageSize <= 0)
        {
            return BadRequest(new ApiResponse(false, "Page and PageSize must be greater than 0."));
        }
        if(pageSize > 100)
        {
            return BadRequest(new ApiResponse(false, "PageSize must be less than or equal to 100."));
        }
        var result = await _mediator.Send(new GetRecentDefectsQuery(page, pageSize));
        return Ok(new ApiResponse(true, "Recent defects retrieved successfully", result));
    }

    [HttpGet("defects-statistics")]
    [Authorize(Roles = "SystemAdmin, Manager, Analyst")]
    public async Task<IActionResult> GetDefectStatistics()
    {
        var result = await _mediator.Send(new GetDefectStatisticsQuery());
        return Ok(new ApiResponse(true, "Defects statistics retrieved successfully", result));
    }

    [HttpGet("mission-status")]
    [Authorize(Roles = "SystemAdmin, Manager, Inspector")]
    public async Task<IActionResult> GetMissionStatus()
    {
        var result = await _mediator.Send(new GetMissionStatusQuery());
        return Ok(new ApiResponse(true, "Mission status retrieved successfully", result));
    }

    [HttpGet("inspections")]
    [Authorize(Roles = "SystemAdmin, Manager, Inspector, Analyst")]
    public async Task<IActionResult> GetInspections(
        [FromQuery] Guid? missionId,
        [FromQuery] bool? isDefect,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10)
    {
        var result = await _mediator.Send(new GetInspectionHistoryQuery(missionId, isDefect, fromDate, toDate, page, pageSize));
        return Ok(new ApiResponse(true, "Inspections retrieved successfully", result));
    }

    [HttpGet("alerts")]
    [Authorize(Roles = "SystemAdmin, Manager, Analyst, Technician")]
    public async Task<IActionResult> GetActiveAlerts()
    {
        var result = await _mediator.Send(new GetActiveAlertsQuery());
        return Ok(new ApiResponse(true, "Active alerts retrieved successfully", result));
    }
}
