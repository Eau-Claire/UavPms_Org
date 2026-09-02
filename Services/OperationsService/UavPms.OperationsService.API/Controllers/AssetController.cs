using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using MediatR;
using System;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System.Threading.Tasks;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Features.Assets.Commands.CreateAsset;
using UavPms.OperationsService.Application.Features.Assets.Commands.UpdateAsset;
using UavPms.OperationsService.Application.Features.Assets.Commands.DeleteAsset;
using UavPms.OperationsService.Application.Features.Assets.DTOs;
using UavPms.OperationsService.Application.Features.Assets.Queries.GetAssets;
using UavPms.OperationsService.Application.Features.Assets.Queries.GetAssetById;
using UavPms.OperationsService.Application.Features.Assets.Queries.GetAssetHealthSummary;
using UavPms.OperationsService.Application.Features.Assets.Queries.SpatialAssets;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/assets")]
[ApiVersion("1.0")]
[Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
public partial class AssetController : ControllerBase
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

    [HttpPost("spatial-query")]
    public async Task<IActionResult> SpatialQuery([FromBody] SpatialQueryRequest request)
    {
        if (!TryCreatePolygon(request.Geometry, out var polygon, out var error))
        {
            return BadRequest(new ApiResponse(false, error));
        }

        var result = await _mediator.Send(new SpatialAssetQuery(polygon!, request.Filters?.ManagementUnitId, request.Filters?.PowerLineId, request.Filters?.AssetType));
        return Ok(new ApiResponse(true, "Spatial assets retrieved successfully.", result));
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

public record SpatialQueryRequest(GeoJsonGeometry? Geometry, SpatialQueryFilters? Filters = null);
public record SpatialQueryFilters(Guid? ManagementUnitId, Guid? PowerLineId, string? AssetType);

public record GeoJsonGeometry(string Type, double[][][] Coordinates);

public partial class AssetController
{
    private static bool TryCreatePolygon(GeoJsonGeometry? geometry, out Polygon? polygon, out string error)
    {
        polygon = null;
        error = "INVALID_GEOMETRY";

        if (geometry is null || !string.Equals(geometry.Type, "Polygon", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (geometry.Coordinates.Length == 0 || geometry.Coordinates[0].Length < 4)
        {
            return false;
        }

        var shell = geometry.Coordinates[0]
            .Select(point => point.Length >= 2 ? new Coordinate(point[0], point[1]) : null)
            .ToArray();

        if (shell.Any(point => point is null))
        {
            return false;
        }

        var coordinates = shell.Select(point => point!).ToList();
        if (!coordinates[0].Equals2D(coordinates[^1])) return false;

        if (coordinates.Any(c => c.X is < -180 or > 180 || c.Y is < -90 or > 90)) return false;

        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        polygon = geometryFactory.CreatePolygon(coordinates.ToArray());
        if (!polygon.IsValid || polygon.IsEmpty)
        {
            polygon = null;
            return false;
        }

        error = string.Empty;
        return true;
    }
}
