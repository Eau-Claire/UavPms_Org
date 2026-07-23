using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using MediatR;
using System;
using System.Threading.Tasks;
using UavPms.OperationsService.Api.Controllers;
using UavPms.OperationsService.Application.Features.TransmissionLines.Commands.CreateTransmissionLine;
using UavPms.OperationsService.Application.Features.TransmissionLines.Commands.UpdateTransmissionLine;
using UavPms.OperationsService.Application.Features.TransmissionLines.Commands.DeleteTransmissionLine;
using UavPms.OperationsService.Application.Features.TransmissionLines.Queries.GetTransmissionLines;
using UavPms.OperationsService.Application.Features.TransmissionLines.Queries.GetTransmissionLinesById;

namespace UavPms.OperationsService.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/lines")]
[ApiVersion("1.0")]
[Authorize]
public class TransmissionLineController : ControllerBase
{
    private readonly ISender _mediator;

    public TransmissionLineController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? substationAssetId = null,
        [FromQuery] string? search = null)
    {
        var query = new GetTransmissionLinesQuery(page, pageSize, substationAssetId, search);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy danh sách đường dây truyền tải thành công.", result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetTransmissionLineByIdQuery(id);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy thông tin đường dây truyền tải thành công.", result));
    }

    [HttpPost]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateTransmissionLineRequest request)
    {
        var command = new CreateTransmissionLineCommand(
            request.SubstationAssetId,
            request.LineName,
            request.IsCriticalEdge,
            request.GeomWkt
        );
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse(true, "Tạo đường dây truyền tải thành công.", result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTransmissionLineRequest request)
    {
        var command = new UpdateTransmissionLineCommand(
            id,
            request.SubstationAssetId,
            request.LineName,
            request.IsCriticalEdge,
            request.GeomWkt
        );
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Cập nhật đường dây truyền tải thành công.", result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var command = new DeleteTransmissionLineCommand(id);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Xóa đường dây truyền tải thành công."));
    }
}

// ==========================================
// CÁC REQUEST DTO KHAI BÁO Ở CUỐI FILE CONTROLLER
// ==========================================

public record CreateTransmissionLineRequest(
    Guid SubstationAssetId,
    string LineName,
    bool IsCriticalEdge,
    string? GeomWkt
);

public record UpdateTransmissionLineRequest(
    Guid SubstationAssetId,
    string LineName,
    bool IsCriticalEdge,
    string? GeomWkt
);
