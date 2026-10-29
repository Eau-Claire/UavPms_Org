using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.FlightControllers;
using UavPms.OperationsService.Application.Features.FlightControllers.DTOs;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/flight-controllers")]
[Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
public sealed class FlightControllersController : ControllerBase
{
    private readonly IFlightControllerService _flightControllerService;

    public FlightControllersController(IFlightControllerService flightControllerService)
    {
        _flightControllerService = flightControllerService;
    }

    [HttpGet("{uid}/drone")]
    public async Task<ActionResult<FlightControllerResolveResponseDto>> ResolveDrone(string uid, CancellationToken ct)
    {
        var result = await _flightControllerService.ResolveByUidAsync(uid, ct);
        return Ok(result);
    }
}
