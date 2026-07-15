using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UavPms.Application.Features.AIAnalysis.Commands.UploadForAnalysis;
using UavPms.Application.Features.AIAnalysis.Commands.AnalyzeMissionMedia;
using UavPms.Application.Features.AIAnalysis.Commands.AnalyzeExistingMedia;
using UavPms.Application.Features.AIAnalysis.Queries.GetAnalysisById;
using UavPms.Core.Enums;

namespace UavPms.WebApi.Controllers;

/// <summary>
/// API cho phân tích AI ad-hoc — không liên kết với mission cụ thể.
/// Hỗ trợ upload nhiều ảnh và/hoặc video trong cùng 1 request.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/ai-analysis")]
[ApiVersion("1.0")]
[Authorize(Roles = "Analyst,Supervisor,SystemAdmin")]
public class AIAnalysisController : ControllerBase
{
    private readonly ISender _mediator;

    public AIAnalysisController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Upload 1 hoặc nhiều ảnh/video để AI phân tích ad-hoc (không cần liên kết mission).
    /// </summary>
    /// <param name="files">Danh sách file (JPEG, PNG, WebP, TIFF, MP4, AVI, MOV)</param>
    /// <param name="analysisType">Loại phân tích AI (optional, default: General)</param>
    /// <param name="notes">Ghi chú (optional)</param>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Upload(
        List<IFormFile> files,
        [FromForm] AnalysisType analysisType = AnalysisType.General,
        [FromForm] string? notes = null)
    {
        // Validate file presence
        if (files == null || files.Count == 0)
        {
            return BadRequest(new ApiResponse(false, "At least one image or video file is required."));
        }

        // Allowed MIME types: images + videos
        var allowedTypes = new[]
        {
            "image/jpeg", "image/png", "image/webp", "image/tiff",
            "video/mp4", "video/x-msvideo", "video/quicktime", "video/webm"
        };

        // Allowed file extensions (defense-in-depth alongside MIME type check)
        var allowedExtensions = new[]
        {
            ".jpg", ".jpeg", ".png", ".webp", ".tiff", ".tif",
            ".mp4", ".avi", ".mov", ".webm"
        };

        // Max file count to prevent abuse
        const int maxFileCount = 20;
        if (files.Count > maxFileCount)
        {
            return BadRequest(new ApiResponse(false, $"Maximum {maxFileCount} files per request."));
        }

        // Max file size: 20MB cho ảnh, 100MB cho video
        const long maxImageSize = 20 * 1024 * 1024;
        const long maxVideoSize = 100 * 1024 * 1024;

        // Validate từng file trước khi xử lý
        var errors = new List<string>();
        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            // Use only the filename portion (strip directory paths from user input)
            var safeFileName = Path.GetFileName(file.FileName);

            if (file.Length == 0)
            {
                errors.Add($"File [{i}] '{safeFileName}': file is empty.");
                continue;
            }

            var contentType = file.ContentType.ToLower();
            if (Array.IndexOf(allowedTypes, contentType) < 0)
            {
                errors.Add($"File [{i}] '{safeFileName}': unsupported type '{contentType}'. Allowed: JPEG, PNG, WebP, TIFF, MP4, AVI, MOV, WebM.");
                continue;
            }

            // Validate file extension matches Content-Type (防 extension spoofing)
            var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || Array.IndexOf(allowedExtensions, extension) < 0)
            {
                errors.Add($"File [{i}] '{safeFileName}': file extension '{extension}' is not allowed.");
                continue;
            }

            var isVideo = contentType.StartsWith("video/");
            var maxSize = isVideo ? maxVideoSize : maxImageSize;
            if (file.Length > maxSize)
            {
                var limitMb = maxSize / (1024 * 1024);
                errors.Add($"File [{i}] '{safeFileName}': exceeds {limitMb}MB limit ({file.Length / (1024 * 1024)}MB).");
            }
        }

        if (errors.Count > 0)
        {
            return BadRequest(new ApiResponse(false, "Validation failed for one or more files.", Errors: errors));
        }

        // Tạo danh sách FileUploadItem
        var fileItems = new List<FileUploadItem>();
        foreach (var file in files)
        {
            fileItems.Add(new FileUploadItem
            {
                FileStream = file.OpenReadStream(),
                FileName = file.FileName,
                ContentType = file.ContentType
            });
        }

        var command = new UploadForAIAnalysisCommand
        {
            Files = fileItems,
            AnalysisType = analysisType,
            Notes = notes
        };

        var results = await _mediator.Send(command);

        // Dispose tất cả stream sau khi xử lý xong
        foreach (var item in fileItems)
        {
            await item.FileStream.DisposeAsync();
        }

        var message = results.Count == 1
            ? "File uploaded for AI analysis successfully."
            : $"{results.Count} files uploaded for AI analysis successfully.";

        return Ok(new ApiResponse(true, message, results));
    }

    /// <summary>
    /// Lấy kết quả phân tích AI theo ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetAIAnalysisByIdQuery(id));
        return Ok(new ApiResponse(true, "AI analysis result retrieved successfully.", result));
    }

    /// <summary>
    /// Upload one or more images/videos for AI analysis linked with a mission.
    /// </summary>
    /// <param name="missionId">Mission ID.</param>
    /// <param name="files">Image and/or video files to analyze.</param>
    /// <param name="analysisType">AI analysis type.</param>
    /// <param name="preferredModel">Preferred AI model or SERVER for server-side selection.</param>
    /// <param name="notes">Optional notes for the batch.</param>
    [HttpPost("/api/v{version:apiVersion}/missions/{missionId:guid}/ai-analysis")]
    [Consumes("multipart/form-data")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> AnalyzeMissionMedia(
        Guid missionId,
        [FromForm] IFormFileCollection files,
        [FromForm] AnalysisType analysisType = AnalysisType.General,
        [FromForm] string preferredModel = "SERVER",
        [FromForm] string? notes = null)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new ApiResponse(false, "Files are required."));
        }

        var fileData = new List<FileDataDto>();
        foreach (var file in files)
        {
            fileData.Add(new FileDataDto
            {
                Stream = file.OpenReadStream(),
                FileName = Path.GetFileName(file.FileName),
                ContentType = file.ContentType
            });
        }

        try
        {
            var command = new AnalyzeMissionMediaCommand
            {
                MissionId = missionId,
                Files = fileData,
                AnalysisType = analysisType,
                PreferredModel = preferredModel,
                Notes = notes
            };

            var result = await _mediator.Send(command);

            return Ok(new ApiResponse(true, "AI analysis batch created and queued for processing.", result));
        }
        finally
        {
            foreach (var item in fileData)
            {
                await item.Stream.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Phân tích AI sử dụng file InspectionMedia đã tồn tại trong mission.
    /// </summary>
    [HttpPost("/api/v{version:apiVersion}/missions/{missionId:guid}/ai-analysis/from-media/{mediaId:guid}")]
    public async Task<IActionResult> AnalyzeExistingMedia(
        Guid missionId,
        Guid mediaId,
        [FromQuery] AnalysisType analysisType = AnalysisType.General,
        [FromQuery] string preferredModel = "SERVER",
        [FromQuery] string? notes = null)
    {
        var command = new AnalyzeExistingMediaCommand
        {
            MissionId = missionId,
            MediaId = mediaId,
            AnalysisType = analysisType,
            PreferredModel = preferredModel,
            Notes = notes
        };

        var result = await _mediator.Send(command);

        return Ok(new ApiResponse(true, "AI analysis request created and queued for processing.", result));
    }
}
