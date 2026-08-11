using System.Threading.Tasks;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Devices.Commands;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/devices")]
[ApiVersion("1.0")]
public class DevicesController : ControllerBase
{
    private readonly ISender _mediator;

    public DevicesController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> SendHeartbeat([FromBody] HeartbeatCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
