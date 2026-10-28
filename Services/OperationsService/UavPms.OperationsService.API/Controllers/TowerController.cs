using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using MediatR;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Features.Towers.Commands;
using UavPms.OperationsService.Application.Features.Towers.Queries;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

/// <summary>
/// Cột điện: đơn vị hạ tầng nhỏ nhất, thuộc trực tiếp một khu vực (ERD v2.2: regions → towers).
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/towers")]
[ApiVersion("1.0")]
[Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
public class TowerController : ControllerBase
{
    private readonly ISender _mediator;

    public TowerController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? regionId = null,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetTowersQuery(page, pageSize, regionId, search));
        return Ok(new ApiResponse(true, "Lấy danh sách cột điện thành công.", result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetTowerByIdQuery(id));
        return Ok(new ApiResponse(true, "Lấy thông tin cột điện thành công.", result));
    }

    /// <summary>
    /// Trang chi tiết cột điện: danh sách bất thường (detected_anomalies) và lịch sử bảo trì, có phân trang,
    /// lọc theo validationStatus (bất thường) và khoảng thời gian tạo, kèm thống kê của cột.
    /// </summary>
    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? validationStatus = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var result = await _mediator.Send(new GetTowerHistoryQuery(id, page, pageSize, validationStatus, from, to));
        return Ok(new ApiResponse(true, "Lấy lịch sử cột điện thành công.", result));
    }

    [HttpPost("spatial-query")]
    public async Task<IActionResult> SpatialQuery([FromBody] SpatialQueryRequest request)
    {
        if (!TryCreatePolygon(request.Geometry, out var polygon, out var error))
        {
            return BadRequest(new ApiResponse(false, error));
        }

        var result = await _mediator.Send(new SpatialTowerQuery(polygon!, request.Filters?.RegionId));
        return Ok(new ApiResponse(true, "Spatial towers retrieved successfully.", result));
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Create([FromBody] CreateTowerRequest request)
    {
        var result = await _mediator.Send(new CreateTowerCommand(
            request.RegionId, request.TowerCode, request.PoleType, request.Address, request.Latitude, request.Longitude));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse(true, "Tạo cột điện thành công.", result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTowerRequest request)
    {
        var result = await _mediator.Send(new UpdateTowerCommand(
            id, request.RegionId, request.TowerCode, request.PoleType, request.Address, request.Latitude, request.Longitude));
        return Ok(new ApiResponse(true, "Cập nhật cột điện thành công.", result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteTowerCommand(id));
        return Ok(new ApiResponse(true, "Xóa cột điện thành công."));
    }

    /// <summary>
    /// Nhập cột điện từ Excel. Cột: mã khu vực (hoặc RegionId), mã cột, vĩ độ, kinh độ, loại cột, địa chỉ.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ApiResponse(false, "Vui lòng chọn tệp Excel hợp lệ."));
        }

        using var stream = file.OpenReadStream();
        var result = await _mediator.Send(new ImportTowersCommand(stream));
        return Ok(new ApiResponse(true, "Nhập dữ liệu cột điện hàng loạt thành công.", result));
    }

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

// ==========================================
// CÁC REQUEST DTO KHAI BÁO Ở CUỐI FILE CONTROLLER
// ==========================================

public record CreateTowerRequest(
    Guid RegionId,
    string TowerCode,
    double Latitude,
    double Longitude,
    string? PoleType = null,
    string? Address = null
);

public record UpdateTowerRequest(
    Guid RegionId,
    string TowerCode,
    double Latitude,
    double Longitude,
    string? PoleType = null,
    string? Address = null
);

public record SpatialQueryRequest(GeoJsonGeometry? Geometry, SpatialQueryFilters? Filters = null);
public record SpatialQueryFilters(Guid? RegionId);
public record GeoJsonGeometry(string Type, double[][][] Coordinates);
