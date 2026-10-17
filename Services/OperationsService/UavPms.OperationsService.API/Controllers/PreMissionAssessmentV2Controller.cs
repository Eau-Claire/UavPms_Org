using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/pre-mission-assessments")]
[Authorize(Roles = UserRoles.AdminManagerInspector)]
public sealed class PreMissionAssessmentV2Controller : ControllerBase
{
    private readonly PreMissionAssessmentService _service;

    public PreMissionAssessmentV2Controller(PreMissionAssessmentService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Create([FromBody] CreateAssessmentRequest request, CancellationToken ct)
    {
        var assessment = await _service.CreateAsync(
            request.RegionId,
            request.PlannedStart,
            request.PlannedEnd,
            request.AssetIds,
            request.BoundaryWkt,
            request.IdempotencyKey,
            ct);
        return Ok(assessment.ToDto());
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var list = await _service.ListAsync(status, ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            list = list.Where(a =>
                (a.Findings != null && a.Findings.ToLowerInvariant().Contains(s)) ||
                (a.EvaluationPolicyVersion != null && a.EvaluationPolicyVersion.ToLowerInvariant().Contains(s))
            ).ToList();
        }

        var pagedList = list.Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToList();
        return Ok(pagedList.Select(a => a.ToDto()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var assessment = await _service.GetAsync(id, ct);
        return Ok(assessment.ToDto());
    }

    [HttpPost("{id:guid}/evaluate")]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct)
    {
        var assessment = await _service.EvaluateAsync(id, ct);
        return Ok(assessment.ToDto());
    }

    [HttpPost("{id:guid}/re-evaluate")]
    public async Task<IActionResult> ReEvaluate(Guid id, CancellationToken ct)
    {
        var assessment = await _service.EvaluateAsync(id, ct);
        return Ok(assessment.ToDto());
    }

    [HttpPost("{id:guid}/create-mission")]
    public async Task<IActionResult> CreateMission(Guid id, [FromBody] CreateMissionFromAssessmentRequest request, CancellationToken ct)
    {
        var finalRequest = request.AssessmentId == id
            ? request
            : request with { AssessmentId = id };

        var mission = await _service.CreateMissionFromAssessmentAsync(finalRequest, ct);
        return Ok(mission);
    }

    [HttpPost("{id:guid}/mark-completed")]
    public async Task<IActionResult> MarkCompleted(Guid id, [FromBody] MarkAssessmentCompletedRequest? request, CancellationToken ct)
    {
        var assessment = await _service.MarkCompletedAsync(id, request?.MissionId, ct);
        return Ok(assessment.ToDto());
    }

    [HttpPost("{id:guid}/consume")]
    public async Task<IActionResult> Consume(Guid id, [FromBody] MarkAssessmentCompletedRequest? request, CancellationToken ct)
    {
        var assessment = await _service.MarkCompletedAsync(id, request?.MissionId, ct);
        return Ok(assessment.ToDto());
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelAssessmentRequest? request, CancellationToken ct)
    {
        var assessment = await _service.CancelAsync(id, request?.Reason, ct);
        return Ok(assessment.ToDto());
    }
}
