using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController, ApiVersion("1.0"), Route("api/v{version:apiVersion}/pre-mission-assessments")]
[Authorize(Roles = UserRoles.AdminAndManager)]
[Obsolete("Use /api/v2/pre-mission-assessments instead.")]
public sealed class PreMissionAssessmentController : ControllerBase
{
    private readonly PreMissionAssessmentService _service;
    public PreMissionAssessmentController(PreMissionAssessmentService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] V1CreateAssessmentRequest request, CancellationToken ct)
    {
        AddSunsetHeader();
        return Ok(await _service.CreateAsync(request.RegionId, request.PlannedStart, request.PlannedEnd, request.AssetIds, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
    {
        AddSunsetHeader();
        return Ok(await _service.ListAsync(status, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        AddSunsetHeader();
        return Ok(await _service.GetAsync(id, ct));
    }

    [HttpPost("{id:guid}/evaluate")]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct)
    {
        AddSunsetHeader();
        return Ok(await _service.EvaluateAsync(id, ct));
    }

    [HttpPost("{id:guid}/re-evaluate")]
    public async Task<IActionResult> ReEvaluate(Guid id, CancellationToken ct)
    {
        AddSunsetHeader();
        return Ok(await _service.EvaluateAsync(id, ct));
    }

    [HttpPost("{id:guid}/create-mission")]
    public async Task<IActionResult> CreateMission(Guid id, [FromBody] V1CreateMissionFromAssessmentRequest request, CancellationToken ct)
    {
        AddSunsetHeader();
        return Ok(await _service.CreateMissionAsync(id, request.Title, request.InspectorId, request.DroneId, ct));
    }

    [HttpPost("{id:guid}/mark-completed")]
    public async Task<IActionResult> MarkCompleted(Guid id, [FromBody] MarkAssessmentCompletedRequest? request, CancellationToken ct)
    {
        AddSunsetHeader();
        return Ok(await _service.MarkCompletedAsync(id, request?.MissionId, ct));
    }

    [HttpPost("{id:guid}/consume")]
    public async Task<IActionResult> Consume(Guid id, [FromBody] MarkAssessmentCompletedRequest? request, CancellationToken ct)
    {
        AddSunsetHeader();
        return Ok(await _service.MarkCompletedAsync(id, request?.MissionId, ct));
    }

    private void AddSunsetHeader()
    {
        Response.Headers.Append("Sunset", "true");
        Response.Headers.Append("Deprecation", "true");
        Response.Headers.Append("Link", "</api/v2/pre-mission-assessments>; rel=\"successor-version\"");
    }
}

public sealed record V1CreateAssessmentRequest(Guid RegionId, DateTime PlannedStart, DateTime PlannedEnd, IReadOnlyCollection<Guid> AssetIds);
public sealed record V1CreateMissionFromAssessmentRequest(string Title, Guid InspectorId, Guid DroneId);
