using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using MediatR;
using System;
using System.Threading.Tasks;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Features.AssetComponents.Commands.CreateAssetComponent;
using UavPms.OperationsService.Application.Features.AssetComponents.Commands.UpdateAssetComponent;
using UavPms.OperationsService.Application.Features.AssetComponents.Commands.DeleteAssetComponent;
using UavPms.OperationsService.Application.Features.AssetComponents.Queries.GetAssetComponents;
using UavPms.OperationsService.Application.Features.AssetComponents.Queries.GetAssetComponentById;
using UavPms.OperationsService.Application.Common.Utilities;
using UavPms.OperationsService.Application.Features.AssetComponents.DTOs;
using UavPms.OperationsService.Application.Features.AssetComponents.Queries.GetAssetHealthSummary;
using UavPms.OperationsService.Application.Features.AssetComponents.Queries.SpatialAssets;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/asset-components")]
[ApiVersion("1.0")]
[Authorize]
public class AssetComponentController : ControllerBase
{
    private readonly ISender _mediator;

    public AssetComponentController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HttpGet("/api/v{version:apiVersion}/assets")]
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
        var query = new GetAssetComponentsQuery(
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
    [HttpGet("/api/v{version:apiVersion}/assets/health-summary")]
    public async Task<IActionResult> GetHealthSummary()
    {
        var result = await _mediator.Send(new GetAssetHealthSummaryQuery());
        return Ok(new ApiResponse(true, "Asset health summary retrieved successfully.", result));
    }

    [HttpGet("{id:guid}")]
    [HttpGet("/api/v{version:apiVersion}/assets/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetAssetComponentByIdQuery(id);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Lấy thông tin chi tiết thiết bị thành công.", result));
    }

    [HttpPost("spatial-query")]
    public async Task<IActionResult> SpatialQuery([FromBody] SpatialQueryRequest request)
    {
        var error = "Invalid GeoJSON Polygon.";
        if (request.Geometry is null
            || !SpatialGeometryFactory.TryCreatePolygon(
                request.Geometry.Type,
                request.Geometry.Coordinates,
                out var polygon,
                out error))
        {
            return BadRequest(new ApiResponse(false, error.Length == 0 ? "Invalid GeoJSON Polygon." : error));
        }

        var result = await _mediator.Send(new SpatialAssetQuery(polygon!));
        return Ok(new ApiResponse(true, "Spatial assets retrieved successfully.", result));
    }

    [HttpGet("nearby")]
    public async Task<IActionResult> Nearby(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double radius)
    {
        if (!SpatialGeometryFactory.IsValidLatitude(lat))
            return BadRequest(new ApiResponse(false, "Latitude must be between -90 and 90."));
        if (!SpatialGeometryFactory.IsValidLongitude(lng))
            return BadRequest(new ApiResponse(false, "Longitude must be between -180 and 180."));
        if (!double.IsFinite(radius) || radius <= 0)
            return BadRequest(new ApiResponse(false, "Radius must be greater than zero meters."));

        var result = await _mediator.Send(new NearbyAssetsQuery(lat, lng, radius));
        return Ok(new ApiResponse(true, "Nearby assets retrieved successfully.", result));
    }

    [HttpGet("map")]
    public async Task<IActionResult> Map(
        [FromQuery] double minLat,
        [FromQuery] double minLng,
        [FromQuery] double maxLat,
        [FromQuery] double maxLng)
    {
        if (!SpatialGeometryFactory.IsValidLatitude(minLat)
            || !SpatialGeometryFactory.IsValidLatitude(maxLat)
            || !SpatialGeometryFactory.IsValidLongitude(minLng)
            || !SpatialGeometryFactory.IsValidLongitude(maxLng))
        {
            return BadRequest(new ApiResponse(false, "Bounding box coordinates are invalid."));
        }

        if (minLat > maxLat || minLng > maxLng)
            return BadRequest(new ApiResponse(false, "Bounding box minimums must not exceed maximums."));

        var result = await _mediator.Send(new MapAssetsQuery(minLat, minLng, maxLat, maxLng));
        return Ok(new ApiResponse(true, "Map assets retrieved successfully.", result));
    }

    [HttpPost]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateAssetComponentRequest request)
    {
        var command = new CreateAssetComponentCommand(request.TowerId, request.ComponentType, request.ComponentCode);
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse(true, "Tạo thiết bị thành công.", result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAssetComponentRequest request)
    {
        var command = new UpdateAssetComponentCommand(
            id,
            request.TowerId,
            request.ComponentType,
            request.ComponentCode,
            request.Status
        );
        var result = await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Cập nhật thiết bị thành công.", result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager,SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var command = new DeleteAssetComponentCommand(id);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Xóa thiết bị thành công."));
    }
}

// ==========================================
// CÁC REQUEST DTO KHAI BÁO Ở CUỐI FILE CONTROLLER
// ==========================================

public record CreateAssetComponentRequest(
    Guid TowerId,
    string ComponentType,
    string ComponentCode
);

public record UpdateAssetComponentRequest(
    Guid TowerId,
    string ComponentType,
    string ComponentCode,
    string Status
);
