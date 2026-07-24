using System;
using System.IO;
using System.Threading.Tasks;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;
using UavPms.OperationsService.Application.Features.Inspections.Queries.GetReportById;
using UavPms.OperationsService.Application.Features.Inspections.Queries.GetByMission;

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
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(
        [FromForm] Guid missionId,
        [FromForm] Guid assetId,
        [FromForm] DateTime capturedAt,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ApiResponse(false, "Image file is required."));
        }

        // Validate file type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/tiff", "video/mp4" };
        if (Array.IndexOf(allowedTypes, file.ContentType.ToLower()) < 0)
        {
            return BadRequest(new ApiResponse(false, "File type not supported. Allowed: JPEG, PNG, WebP, TIFF, MP4."));
        }

        // Validate file size (max 50MB)
        const long maxSize = 50 * 1024 * 1024;
        if (file.Length > maxSize)
        {
            return BadRequest(new ApiResponse(false, "File size exceeds the 50MB limit."));
        }

        await using var stream = file.OpenReadStream();

        var command = new UploadInspectionImageCommand
        {
            MissionId = missionId,
            AssetId = assetId,
            CapturedAt = capturedAt,
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType
        };

        var result = await _mediator.Send(command);

        return Ok(new ApiResponse(true, "Image uploaded successfully.", result));
    }

    /// <summary>
    /// Lấy báo cáo kiểm tra theo ID
    /// </summary>
    [HttpGet("report/{id:guid}")]
    public async Task<IActionResult> GetReportById(Guid id)
    {
        var result = await _mediator.Send(new GetInspectionReportByIdQuery(id));
        return Ok(new ApiResponse(true, "Inspection report retrieved successfully.", result));
    }

    /// <summary>
    /// Lấy lịch sử kiểm tra theo mã nhiệm vụ
    /// </summary>
    [HttpGet("mission/{missionId:guid}")]
    public async Task<IActionResult> GetByMission(Guid missionId)
    {
        var result = await _mediator.Send(new GetInspectionsByMissionQuery(missionId));
        return Ok(new ApiResponse(true, "Mission inspection history retrieved successfully.", result));
    }
}
