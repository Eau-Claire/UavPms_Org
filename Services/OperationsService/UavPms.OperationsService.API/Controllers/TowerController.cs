using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using MediatR;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Features.Towers.Commands.CreateTower;
using UavPms.OperationsService.Application.Features.Towers.Commands.UpdateTower;
using UavPms.OperationsService.Application.Features.Towers.Commands.DeleteTower;
using UavPms.OperationsService.Application.Features.Towers.Commands.ImportTowers;
using UavPms.OperationsService.Application.Features.Towers.Queries.GetTowers;
using UavPms.OperationsService.Application.Features.Towers.Queries.GetTowerById;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/towers")]
[ApiVersion("1.0")]
[Authorize]
public class TowerController : ControllerBase
{
    private readonly ISender _mediator;

    public TowerController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] Guid? lineAssetId = null)
    {
        var query = new GetTowersQuery(page, pageSize, lineAssetId);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy danh sách cột điện thành công.", result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetTowerByIdQuery(id);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy thông tin cột điện thành công.", result));
    }

    [HttpPost]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateTowerRequest request)
    {
        var command = new CreateTowerCommand(
            request.LineAssetId,
            request.TowerCode,
            request.Latitude,
            request.Longitude
        );
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse(true, "Tạo cột điện thành công.", result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTowerRequest request)
    {
        var command = new UpdateTowerCommand(
            id,
            request.LineAssetId,
            request.TowerCode,
            request.Latitude,
            request.Longitude
        );
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Cập nhật cột điện thành công.", result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var command = new DeleteTowerCommand(id);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Xóa cột điện thành công."));
    }

    [HttpPost("import")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ApiResponse(false, "Vui lòng chọn tệp Excel hợp lệ."));
        }

        using var stream = file.OpenReadStream();
        var command = new ImportTowersCommand(stream);
        var result = await _mediator.Send(command);

        return Ok(new ApiResponse(true, "Nhập dữ liệu cột điện hàng loạt thành công.", result));
    }
}

// ==========================================
// CÁC REQUEST DTO KHAI BÁO Ở CUỐI FILE CONTROLLER
// ==========================================

public record CreateTowerRequest(
    Guid LineAssetId,
    string TowerCode,
    double Latitude,
    double Longitude
);

public record UpdateTowerRequest(
    Guid LineAssetId,
    string TowerCode,
    double Latitude,
    double Longitude
);
