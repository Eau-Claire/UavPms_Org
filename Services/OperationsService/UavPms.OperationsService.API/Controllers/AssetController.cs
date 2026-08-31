using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using MediatR;
using System;
using System.Threading.Tasks;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Features.Assets.Commands.CreateAsset;
using UavPms.OperationsService.Application.Features.Assets.Commands.UpdateAsset;
using UavPms.OperationsService.Application.Features.Assets.Commands.DeleteAsset;
using UavPms.OperationsService.Application.Features.Assets.Queries.GetAssets;
using UavPms.OperationsService.Application.Features.Assets.Queries.GetAssetById;
using UavPms.OperationsService.Application.Features.Assets.Queries.GetAssetHealthSummary;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/assets")]
[ApiVersion("1.0")]
[Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
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
        [FromQuery] string? status = null,
        [FromQuery] string[]? riskLevel = null,
        [FromQuery] double? minHealthScore = null,
        [FromQuery] double? maxHealthScore = null,
        [FromQuery] Guid? regionId = null,
        [FromQuery] Guid? lineId = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null)
    {
        var query = new GetAssetsQuery(
            page,
            pageSize,
            towerId,
            assetType,
            status,
            riskLevel,
            minHealthScore,
            maxHealthScore,
            regionId,
            lineId,
            sortBy,
            sortOrder);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy danh sách thiết bị thành công.", result));
    }

    [HttpGet("health-summary")]
    public async Task<IActionResult> GetHealthSummary()
    {
        var result = await _mediator.Send(new GetAssetHealthSummaryQuery());
        return Ok(new ApiResponse(true, "Asset health summary retrieved successfully.", result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetAssetByIdQuery(id);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy thông tin chi tiết thiết bị thành công.", result));
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Create([FromBody] CreateAssetRequest request)
    {
        var command = new CreateAssetCommand(request.TowerId, request.AssetType, request.AssetCode);
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse(true, "Tạo thiết bị thành công.", result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
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
    [Authorize(Roles = UserRoles.AdminAndManager)]
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
