using System;
using System.IO;
using System.Threading.Tasks;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UavPms.Application.Features.Inspections.Commands.UploadImage;

namespace UavPms.WebApi.Controllers;

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
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType
        };

        var result = await _mediator.Send(command);

        return Ok(new ApiResponse(true, "Image uploaded successfully.", result));
    }
}
