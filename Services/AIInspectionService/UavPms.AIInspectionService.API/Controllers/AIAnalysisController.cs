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
}
