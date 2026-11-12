using System;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Schedules;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/inspection-schedules")]
[Authorize]
public sealed class InspectionSchedulesController : ControllerBase
{
    private readonly IInspectionScheduleService _scheduleService;

    public InspectionSchedulesController(IInspectionScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    [HttpGet]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? regionId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _scheduleService.GetPagedAsync(page, pageSize, regionId, isActive, search, ct);
        return Ok(new ApiResponse(true, "Inspection schedules retrieved successfully.", result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _scheduleService.GetByIdAsync(id, ct);
        return Ok(new ApiResponse(true, "Inspection schedule retrieved successfully.", result));
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Create([FromBody] CreateInspectionScheduleRequest request, CancellationToken ct = default)
    {
        var result = await _scheduleService.CreateAsync(
            request.RegionId,
            request.Name,
            request.RecurrenceRule,
            request.PlannedTime,
            request.IsActive,
            ct);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            new ApiResponse(true, "Inspection schedule created successfully.", result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInspectionScheduleRequest request, CancellationToken ct = default)
    {
        var result = await _scheduleService.UpdateAsync(
            id,
            request.RegionId,
            request.Name,
            request.RecurrenceRule,
            request.PlannedTime,
            request.IsActive,
            ct);
        return Ok(new ApiResponse(true, "Inspection schedule updated successfully.", result));
    }

    [HttpPatch("{id:guid}/toggle")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Toggle(Guid id, [FromBody] ToggleInspectionScheduleRequest? request = null, CancellationToken ct = default)
    {
        var result = await _scheduleService.ToggleActiveAsync(id, request?.IsActive, ct);
        return Ok(new ApiResponse(true, "Inspection schedule status updated successfully.", result));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct = default)
    {
        var result = await _scheduleService.ToggleActiveAsync(id, true, ct);
        return Ok(new ApiResponse(true, "Inspection schedule activated successfully.", result));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct = default)
    {
        var result = await _scheduleService.ToggleActiveAsync(id, false, ct);
        return Ok(new ApiResponse(true, "Inspection schedule deactivated successfully.", result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _scheduleService.DeleteAsync(id, ct);
        return Ok(new ApiResponse(true, "Inspection schedule deleted successfully."));
    }

    [HttpPost("{id:guid}/trigger")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Trigger(Guid id, CancellationToken ct = default)
    {
        var result = await _scheduleService.TriggerAsync(id, ct);
        return Ok(new ApiResponse(true, "Inspection schedule triggered successfully.", result));
    }
}

public record CreateInspectionScheduleRequest(
    Guid RegionId,
    string Name,
    string RecurrenceRule,
    TimeOnly PlannedTime,
    bool IsActive = true);

public record UpdateInspectionScheduleRequest(
    Guid RegionId,
    string Name,
    string RecurrenceRule,
    TimeOnly PlannedTime,
    bool IsActive = true);

public record ToggleInspectionScheduleRequest(bool? IsActive = null);

