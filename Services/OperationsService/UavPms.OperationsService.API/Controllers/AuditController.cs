using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.AuditLogs.Queries.GetAuditLogs;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/audit-logs")]
[ApiVersion("1.0")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly ISender _mediator;
    
    public AuditController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? tableName = null,
        [FromQuery] string? actionType = null)
    {
        if (page <= 0 || pageSize <= 0)
        {
            return BadRequest(new ApiResponse(false, "Page and PageSize must be positive integers."));
        }

        if (pageSize > 100)
        {
            return BadRequest(new ApiResponse(false, "Page size must be less than or equal 100."));
        }

        var query = new GetAuditLogsQuery(page, pageSize, search, tableName, actionType);
        var result = await _mediator.Send(query);
        
        return Ok(new ApiResponse(true, "Audit logs retrieved successfully", result));
    }
}