using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.ActiveLearningService.API.Labels;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.ActiveLearningService.API.Controllers;

public record ApiResponse(bool Success, string Message, object? Data = null, object? Errors = null, string? ErrorCode = null);

/// <summary>Whole-image labels for active learning (the gateway routes /anomalies/{id}/labels* here).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/anomalies/{id:guid}/labels")]
[Authorize]
public class AnomalyLabelsController(LabelService labels) : ControllerBase
{
    /// <summary>Saved label set of the anomaly's image/frame; 404 when nothing was saved yet.</summary>
    [HttpGet]
    [Authorize(Roles = UserRoles.AdminManagerAnalyst)]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => Run(async () =>
    {
        var result = await labels.GetAsync(id, UserId, NeedsAssignment, ct);
        return result == null
            ? NotFound(new ApiResponse(false, "No labels saved for this image yet.", ErrorCode: "LABELS_NOT_FOUND"))
            : Ok(new ApiResponse(true, "Labels retrieved.", result));
    });

    /// <summary>Save (upsert) the whole-image label set; isFullyReviewed=false keeps it as a draft.</summary>
    [HttpPut]
    [Authorize(Roles = AdminOrAnalyst)]
    public Task<IActionResult> Save(Guid id, [FromBody] SaveLabelsRequest request, CancellationToken ct) => Run(async () =>
        Ok(new ApiResponse(true, "Labels saved.", await labels.SaveAsync(id, request, UserId, NeedsAssignment, ct))));

    /// <summary>Queue a fully reviewed label set for the next training batch.</summary>
    [HttpPost("submit-training")]
    [Authorize(Roles = AdminOrAnalyst)]
    public Task<IActionResult> Submit(Guid id, [FromBody] SubmitTrainingRequest request, CancellationToken ct) => Run(async () =>
    {
        var result = await labels.SubmitAsync(id, request, UserId, NeedsAssignment, ct);
        return Ok(new ApiResponse(result.Accepted, result.Message, result));
    });

    private const string AdminOrAnalyst = UserRoles.SystemAdmin + "," + UserRoles.Analyst;

    /// <summary>SystemAdmin has full access; an analyst must be assigned to the mission; a manager only reads.</summary>
    private bool NeedsAssignment => !User.IsInRole(UserRoles.SystemAdmin) && User.IsInRole(UserRoles.Analyst);

    private Guid UserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        if (UserId == Guid.Empty)
            return Unauthorized(new ApiResponse(false, "Missing user id in token.", ErrorCode: "UNAUTHORIZED"));
        try
        {
            return await action();
        }
        catch (LabelException ex)
        {
            return StatusCode(ex.StatusCode, new ApiResponse(false, ex.Message, ErrorCode: ex.ErrorCode));
        }
    }
}
