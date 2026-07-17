using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using MediatR;
using System;
using System.Threading.Tasks;
using UavPms.WebApi.Controllers;
using UavPms.Application.Features.Substations.Commands.CreateSubstation;
using UavPms.Application.Features.Substations.Commands.UpdateSubstation;
using UavPms.Application.Features.Substations.Commands.DeleteSubstation;
using UavPms.Application.Features.Substations.Queries.GetSubstation;
using UavPms.Application.Features.Substations.Queries.GetSubstationById;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/substations")]
[ApiVersion("1.0")]
[Authorize]
public class SubstationController : ControllerBase
{
    private readonly ISender _mediator;

    public SubstationController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] Guid? regionAssetId = null, 
        [FromQuery] string? search = null)
    {
        var query = new GetSubstaionQuery(page, pageSize, regionAssetId, search);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy danh sách trạm biến áp thành công.", result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetSubstationByIdQuery(id);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy thông tin trạm biến áp thành công.", result));
    }

    [HttpPost]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateSubstationRequest request)
    {
        var command = new CreateSubstationCommand(
            request.RegionAssetId,
            request.SubstationName,
            request.VoltageLevel,
            request.Latitude,
            request.Longitude
        );
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse(true, "Tạo trạm biến áp thành công.", result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubstationRequest request)
    {
        var command = new UpdateSubstationCommand(
            id,
            request.RegionAssetId,
            request.SubstationName,
            request.VoltageLevel,
            request.Latitude,
            request.Longitude
        );
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Cập nhật trạm biến áp thành công.", result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var command = new DeleteSubstationCommand(id);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Xóa trạm biến áp thành công."));
    }
}

// ==========================================
// CÁC REQUEST DTO KHAI BÁO Ở CUỐI FILE CONTROLLER
// ==========================================

public record CreateSubstationRequest(
    Guid RegionAssetId,
    string SubstationName,
    string VoltageLevel,
    double? Latitude,
    double? Longitude
);

public record UpdateSubstationRequest(
    Guid RegionAssetId,
    string SubstationName,
    string VoltageLevel,
    double? Latitude,
    double? Longitude
);
