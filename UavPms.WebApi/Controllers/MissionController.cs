using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.Application.Features.Missions.Commands.CreateMission;
using UavPms.Application.Features.Missions.Commands.DeleteMission;
using UavPms.Application.Features.Missions.Commands.UpdateMission;
using UavPms.Application.Features.Missions.Queries.GetMissionDetails;
using UavPms.Application.Features.Missions.Queries.GetMyMissions;
using UavPms.Application.Features.Missions.Queries.ListMissions;

namespace UavPms.WebApi.Controllers;

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
    [Authorize(Roles = "SystemAdmin, Manager")]
    public async Task<IActionResult> Create([FromBody] CreateMissionCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Mission created successfully", result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin, Manager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMissionRequest request)
    {
        var command = new UpdateMissionCommand(
            id,
            request.Title,
            request.RouteData,
            request.AssignedToUserId,
            request.DroneCode,
            request.Status,
            request.Description);
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Mission updated successfully", result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin, Manager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteMissionCommand(id));
        return Ok(new ApiResponse(true, "Mission deleted successfully"));
    }

    [HttpGet]
    [Authorize(Roles = "SystemAdmin, Manager")]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        if (page <= 0 || pageSize <= 0)
        {
            return BadRequest(new ApiResponse(false, "Invalid page or page size"));
        }

        if (pageSize > 100)
        {
            return BadRequest(new ApiResponse(false, "Invalid page or page size"));
        }
        
        var query = new ListMissionsQuery(page,  pageSize, search, status);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Mission list retrieved successfully", result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SystemAdmin, Manager")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _mediator.Send(new GetMissionDetailsQuery(id));
        return Ok(new ApiResponse(true, "Mission details retrieved successfully", result));
    }

    [HttpGet("my")]
    [Authorize(Roles = "Inspector")]
    public async Task<IActionResult> GetMyMissions()
    {
        var result = await _mediator.Send(new GetMyMissionsQuery());
        return Ok(new ApiResponse(true, "Missions retrieved successfully", result));
    }
}

public record UpdateMissionRequest(
    string Title,
    string RouteData,
    Guid AssignedToUserId,
    string DroneCode,
    string Status,
    string? Description);