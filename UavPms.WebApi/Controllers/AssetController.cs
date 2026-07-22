using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using MediatR;
using System;
using System.Threading.Tasks;
using UavPms.WebApi.Controllers;
using UavPms.Application.Features.Assets.Commands.CreateAsset;
using UavPms.Application.Features.Assets.Commands.UpdateAsset;
using UavPms.Application.Features.Assets.Commands.DeleteAsset;
using UavPms.Application.Features.Assets.Queries.GetAssets;
using UavPms.Application.Features.Assets.Queries.GetAssetById;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/assets")]
[ApiVersion("1.0")]
[Authorize]
public class AssetController : ControllerBase
{
    private readonly ISender _mediator;

    public AssetController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] Guid? towerId = null, 
        [FromQuery] string? assetType = null, 
        [FromQuery] string? status = null)
    {
        var query = new GetAssetsQuery(page, pageSize, towerId, assetType, status);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy danh sách thiết bị thành công.", result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetAssetByIdQuery(id);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy thông tin chi tiết thiết bị thành công.", result));
    }

    [HttpPost]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateAssetRequest request)
    {
        var command = new CreateAssetCommand(request.TowerId, request.AssetType, request.AssetCode);
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse(true, "Tạo thiết bị thành công.", result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAssetRequest request)
    {
        var command = new UpdateAssetCommand(
            id,
            request.TowerId,
            request.AssetType,
            request.AssetCode,
            request.Status,
            request.CurrentHealthScore,
            request.RiskLevel
        );
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Cập nhật thiết bị thành công.", result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var command = new DeleteAssetCommand(id);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Xóa thiết bị thành công."));
    }
}

// ==========================================
// CÁC REQUEST DTO KHAI BÁO Ở CUỐI FILE CONTROLLER
// ==========================================

public record CreateAssetRequest(
    Guid TowerId,
    string AssetType,
    string AssetCode
);

public record UpdateAssetRequest(
    Guid TowerId,
    string AssetType,
    string AssetCode,
    string Status,
    double CurrentHealthScore,
    string RiskLevel
);
