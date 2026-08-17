using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Drones.Queries.GetDrones;
using UavPms.OperationsService.Application.Features.Drones.Queries.GetDroneStatus;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/drones")]
[ApiVersion("1.0")]
public class DronesController : ControllerBase
{
    private readonly ISender _mediator;

    public DronesController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetDrones()
    {
        return Ok(await _mediator.Send(new GetDronesQuery()));
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableDrones()
    {
        return Ok(await _mediator.Send(new GetDronesQuery(AvailableOnly: true)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDrone(Guid id)
    {
        return Ok(await _mediator.Send(new GetDroneStatusQuery(id)));
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetDroneStatus(Guid id)
    {
        return Ok(await _mediator.Send(new GetDroneStatusQuery(id)));
    }
}
