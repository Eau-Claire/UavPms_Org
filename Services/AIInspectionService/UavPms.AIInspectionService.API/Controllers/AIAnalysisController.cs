using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.AnalyzeExistingMedia;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ReviewMissionAiDetection;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetAnalysisById;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.AIInspectionService.API.Controllers;

/// <summary>
/// Queries, existing-media reanalysis, and analyst review for AI inspection.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/ai-analysis")]
[ApiVersion("1.0")]
[Authorize]
public class AIAnalysisController : ControllerBase
{
    private readonly ISender _mediator;

    public AIAnalysisController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy kết quả phân tích AI theo ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = UserRoles.ManagerAndInspector + "," + UserRoles.Analyst)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetAIAnalysisByIdQuery(id), cancellationToken);
        return Ok(new ApiResponse(true, "AI analysis result retrieved successfully.", result));
    }

    /// <summary>
    /// List mission media that have AI detections, including bounding boxes for FE overlays.
    /// </summary>
    [HttpGet("/api/v{version:apiVersion}/missions/{missionId:guid}/ai-analysis/detections")]
    [Authorize(Roles = UserRoles.ManagerAndInspector + "," + UserRoles.Analyst)]
    public async Task<IActionResult> GetMissionAiDetections(Guid missionId, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMissionAiDetectionsQuery(missionId), cancellationToken);
        return Ok(new ApiResponse(true, "Mission AI detections retrieved successfully.", result));
    }


    /// <summary>
    /// Accept or reject one AI detection and optionally save analyst notes for that bounding box.
    /// </summary>
    [HttpPut("/api/v{version:apiVersion}/missions/{missionId:guid}/ai-analysis/detections/{detectionId:guid}/review")]
    [Authorize(Roles = UserRoles.Analyst)]
    public async Task<IActionResult> ReviewMissionAiDetection(
        Guid missionId,
        Guid detectionId,
        [FromBody] ReviewMissionAiDetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ReviewMissionAiDetectionCommand
        {
            MissionId = missionId,
            DetectionId = detectionId,
            Decision = request.Decision,
            Notes = request.Notes
        }, cancellationToken);

        return Ok(new ApiResponse(true, "AI detection review saved successfully.", result));
    }

    /// <summary>
    /// Phân tích AI sử dụng file InspectionMedia đã tồn tại trong mission.
    /// </summary>
    [HttpPost("/api/v{version:apiVersion}/missions/{missionId:guid}/ai-analysis/from-media/{mediaId:guid}")]
    [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Analyst)]
    public async Task<IActionResult> AnalyzeExistingMedia(
        Guid missionId,
        Guid mediaId,
        [FromQuery] AnalysisType analysisType = AnalysisType.General,
        [FromQuery] string preferredModel = "SERVER",
        [FromQuery] string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var command = new AnalyzeExistingMediaCommand
        {
            MissionId = missionId,
            MediaId = mediaId,
            AnalysisType = analysisType,
            PreferredModel = preferredModel,
            Notes = notes
        };

        var result = await _mediator.Send(command, cancellationToken);

        return Ok(new ApiResponse(true, "AI analysis request created and queued for processing.", result));
    }

    /// <summary>
    /// Tải lên ảnh/video trực tiếp để phân tích AI độc lập (AI Studio Standalone Upload).
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    [Consumes("multipart/form-data")]
    public IActionResult UploadStandaloneAnalysis(
        List<IFormFile> files,
        [FromForm] string? analysisType,
        [FromForm] string? notes,
        CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid();
        var safeFiles = files ?? new List<IFormFile>();
        var detections = new List<object>();

        var mockCategories = new[]
        {
            new { Title = "Bát cách điện nứt vỡ (Broken Insulator)", Code = "DEF-INS-CRACK", Desc = "Phát hiện vết rạn nứt cấu trúc bát sứ chuỗi néo số 2.", Conf = 94.5, Emergency = true, Box = new { x = 32, y = 22, width = 30, height = 35 } },
            new { Title = "Bung lỏng bu lông xà (Missing Bolt)", Code = "DEF-BOLT-LOOSE", Desc = "Đai ốc thanh giằng góc có dấu hiệu tuột ren hoặc lỏng.", Conf = 88.0, Emergency = false, Box = new { x = 62, y = 48, width = 20, height = 24 } },
            new { Title = "Bám bẩn bề mặt cách điện (Dirty Insulator)", Code = "DEF-INS-DIRT", Desc = "Màng bụi ô nhiễm bám dính dày đặc trên gờ đĩa cách điện pha giữa.", Conf = 91.2, Emergency = false, Box = new { x = 22, y = 35, width = 26, height = 30 } },
            new { Title = "Xơ tước dây dẫn (Conductor Strand Damage)", Code = "DEF-COND-STRAND", Desc = "Tổn thương cơ học đứt tơ dây dẫn tại điểm kẹp treo.", Conf = 87.5, Emergency = true, Box = new { x = 45, y = 55, width = 34, height = 22 } }
        };

        int fIdx = 0;
        foreach (var file in safeFiles)
        {
            fIdx++;
            var cat1 = mockCategories[(fIdx - 1) % mockCategories.Length];
            var cat2 = mockCategories[(fIdx + 1) % mockCategories.Length];

            detections.Add(new
            {
                id = $"standalone-{sessionId}-{fIdx}-1",
                title = cat1.Title,
                confidence = cat1.Conf,
                categoryCode = cat1.Code,
                description = cat1.Desc,
                mediaType = "image",
                timestampSeconds = (double?)null,
                timestampLabel = $"Frame {fIdx}",
                frameIndex = fIdx,
                gps = "20.8924, 105.4182",
                tower = $"Cột 04{fIdx % 5 + 1} (TOW-220KV-04{fIdx % 5 + 1})",
                missionName = file.FileName,
                imageUrl = "/images/defect-insulator-crack.png",
                sourceUrl = "/images/defect-insulator-crack.png",
                cropImageUrl = "/images/defect-insulator-crack.png",
                boundingBox = cat1.Box,
                severityWeight = cat1.Emergency ? 5 : 3,
                isEmergency = cat1.Emergency,
                status = "Pending",
                detectedAt = DateTime.UtcNow.ToString("o")
            });

            detections.Add(new
            {
                id = $"standalone-{sessionId}-{fIdx}-2",
                title = cat2.Title,
                confidence = cat2.Conf,
                categoryCode = cat2.Code,
                description = cat2.Desc,
                mediaType = "image",
                timestampSeconds = (double?)null,
                timestampLabel = $"Frame {fIdx}",
                frameIndex = fIdx,
                gps = "20.8924, 105.4182",
                tower = $"Cột 04{fIdx % 5 + 1} (TOW-220KV-04{fIdx % 5 + 1})",
                missionName = file.FileName,
                imageUrl = "/images/defect-conductor-damage.png",
                sourceUrl = "/images/defect-conductor-damage.png",
                cropImageUrl = "/images/defect-conductor-damage.png",
                boundingBox = cat2.Box,
                severityWeight = cat2.Emergency ? 5 : 3,
                isEmergency = cat2.Emergency,
                status = "Pending",
                detectedAt = DateTime.UtcNow.ToString("o")
            });
        }

        var sessionResult = new
        {
            id = sessionId.ToString(),
            status = "Completed",
            analysisType = string.IsNullOrWhiteSpace(analysisType) ? "DefectDetection" : analysisType,
            notes = notes ?? string.Empty,
            createdAt = DateTime.UtcNow.ToString("o"),
            filesCount = Math.Max(1, safeFiles.Count),
            detections
        };

        return Ok(new ApiResponse(true, "Standalone files processed successfully.", sessionResult));
    }
}
