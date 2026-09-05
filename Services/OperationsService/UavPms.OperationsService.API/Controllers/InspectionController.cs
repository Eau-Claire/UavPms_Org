using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;
using UavPms.OperationsService.Application.Features.Inspections.Queries.GetReportById;
using UavPms.OperationsService.Application.Features.Inspections.Queries.GetByMission;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/inspections")]
[ApiVersion("1.0")]
[Authorize]
public class InspectionController : ControllerBase
{
    private readonly ISender _mediator;

    public InspectionController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Upload ảnh kiểm tra chuyến bay
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Roles = UserRoles.InspectorOnly)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(
        [FromForm] Guid missionId,
        [FromForm] Guid assetId,
        [FromForm] DateTime capturedAt,
        [FromForm] double? latitude,
        [FromForm] double? longitude,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        await using var stream = file?.OpenReadStream() ?? Stream.Null;

        var command = new UploadInspectionImageCommand
        {
            MissionId = missionId,
            AssetId = assetId,
            CapturedAt = capturedAt,
            Latitude = latitude,
            Longitude = longitude,
            FileStream = stream,
            FileName = file?.FileName ?? string.Empty,
            ContentType = file?.ContentType ?? string.Empty,
        };

        var result = await _mediator.Send(command, cancellationToken);

        return Ok(new ApiResponse(true, "Image uploaded successfully.", result));
    }

    /// <summary>
    /// Lấy báo cáo kiểm tra theo ID
    /// </summary>
    [HttpGet("report/{id:guid}")]
    [Authorize(Roles = UserRoles.AdminManagerInspectorAnalyst)]
    public async Task<IActionResult> GetReportById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetInspectionReportByIdQuery(id), cancellationToken);
        return Ok(new ApiResponse(true, "Inspection report retrieved successfully.", result));
    }

    /// <summary>
    /// Lấy lịch sử kiểm tra theo mã nhiệm vụ
    /// </summary>
    [HttpGet("mission/{missionId:guid}")]
    [Authorize(Roles = UserRoles.AdminManagerInspectorAnalyst)]
    public async Task<IActionResult> GetByMission(Guid missionId, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetInspectionsByMissionQuery(missionId), cancellationToken);
        return Ok(new ApiResponse(true, "Mission inspection history retrieved successfully.", result));
    }
}
