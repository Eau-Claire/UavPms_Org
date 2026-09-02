using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Missions.Commands.CreateMission;
using UavPms.OperationsService.Application.Features.Missions.Commands.DeleteMission;
using UavPms.OperationsService.Application.Features.Missions.Commands.UpdateMission;
using UavPms.OperationsService.Application.Features.Missions.Queries.GetMissionDetails;
using UavPms.OperationsService.Application.Features.Missions.Queries.GetMyMissions;
using UavPms.OperationsService.Application.Features.Missions.Queries.ListMissions;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/missions")]
[ApiVersion("1.0")]
[Authorize]
public class MissionController : ControllerBase
{
    private readonly ISender _mediator;

    public MissionController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Create([FromBody] CreateMissionRequest request, CancellationToken cancellationToken = default)
    {
        var command = new CreateMissionCommand(
            request.Title ?? request.Name ?? string.Empty,
            request.RouteData,
            request.AssignedToUserId ?? request.InspectorId ?? Guid.Empty,
            request.DroneCode,
            request.Status,
            request.Description,
            request.ScheduledStartAt ?? request.ScheduledAt,
            request.InspectorId,
            request.UavId ?? request.DroneId,
            request.TargetAssetIds);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new ApiResponse(true, "Mission created successfully", result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = UserRoles.ManagerAndInspector)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMissionRequest request, CancellationToken cancellationToken = default)
    {
        var command = new UpdateMissionCommand(
            id,
            request.Title,
            request.RouteData,
            request.AssignedToUserId,
            request.DroneCode,
            request.Status,
            request.Description);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new ApiResponse(true, "Mission updated successfully", result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = UserRoles.ManagerAndInspector)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        await _mediator.Send(new DeleteMissionCommand(id), cancellationToken);
        return Ok(new ApiResponse(true, "Mission deleted successfully"));
    }

    [HttpGet]
    [Authorize(Roles = UserRoles.AdminManagerAnalyst)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sortBy = "createdAt",
        [FromQuery] bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0 || pageSize <= 0)
        {
            return BadRequest(new ApiResponse(false, "Invalid page or page size"));
        }

        if (pageSize > 100)
        {
            return BadRequest(new ApiResponse(false, "Invalid page or page size"));
        }
        
        var query = new ListMissionsQuery(page, pageSize, search, status, sortBy, sortDescending);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(new ApiResponse(true, "Mission list retrieved successfully", result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminManagerInspectorAnalyst)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMissionDetailsQuery(id), cancellationToken);
        return Ok(new ApiResponse(true, "Mission details retrieved successfully", result));
    }

    [HttpGet("my")]
    [Authorize(Roles = UserRoles.InspectorOnly)]
    public async Task<IActionResult> GetMyMissions(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyMissionsQuery(), cancellationToken);
        return Ok(new ApiResponse(true, "Missions retrieved successfully", result));
    }
}

public record CreateMissionRequest(
    string? Title,
    string? Name,
    string? RouteData,
    Guid? AssignedToUserId,
    string? DroneCode,
    string? Status,
    string? Description,
    DateTime? ScheduledStartAt,
    DateTime? ScheduledAt,
    Guid? InspectorId,
    Guid? UavId,
    Guid? DroneId,
    IReadOnlyList<Guid>? TargetAssetIds);

public record UpdateMissionRequest(
    string Title,
    string RouteData,
    Guid AssignedToUserId,
    string DroneCode,
    string Status,
    string? Description);
