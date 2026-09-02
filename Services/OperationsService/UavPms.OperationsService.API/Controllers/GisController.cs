using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Gis.Infrastructure;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController, ApiVersion("1.0"), Route("api/v{version:apiVersion}/gis"), Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
public class GisController(ISender mediator) : ControllerBase
{
    [HttpGet("infrastructure")]
    public async Task<IActionResult> Infrastructure([FromQuery] Guid? administrativeAreaId, [FromQuery] Guid? managementUnitId,
        [FromQuery] Guid? powerLineId, [FromQuery] string? voltageLevel, [FromQuery] string? assetType,
        [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GisInfrastructureQuery(administrativeAreaId, managementUnitId, powerLineId, voltageLevel, assetType, status), cancellationToken);
        return Ok(new ApiResponse(true, "GIS infrastructure retrieved successfully.", result));
    }
}
