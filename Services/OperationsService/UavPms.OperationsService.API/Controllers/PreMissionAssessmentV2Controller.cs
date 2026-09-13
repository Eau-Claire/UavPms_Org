using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/pre-mission-assessments")]
[Authorize(Roles = UserRoles.AdminAndManager)]
public sealed class PreMissionAssessmentV2Controller : ControllerBase
{
    private readonly PreMissionAssessmentService _service;

    public PreMissionAssessmentV2Controller(PreMissionAssessmentService service)
    {
        _service = service;
    }

    [HttpPost]
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
        return Ok(assessment);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = await _service.ListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var assessment = await _service.GetAsync(id, ct);
        return Ok(assessment);
    }

    [HttpPost("{id:guid}/evaluate")]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct)
    {
        var assessment = await _service.EvaluateAsync(id, ct);
        return Ok(assessment);
    }

    [HttpPost("{id:guid}/re-evaluate")]
    public async Task<IActionResult> ReEvaluate(Guid id, CancellationToken ct)
    {
        var assessment = await _service.EvaluateAsync(id, ct);
        return Ok(assessment);
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
}
