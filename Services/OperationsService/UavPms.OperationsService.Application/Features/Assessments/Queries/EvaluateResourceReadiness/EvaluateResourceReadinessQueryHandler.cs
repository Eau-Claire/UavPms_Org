using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments.Queries.EvaluateResourceReadiness;

/// <summary>
/// GET /pre-mission-assessments/{id}/resource-readiness is read-only (BR-07 / BR-17): it returns the stored
/// candidates and status of the last evaluation and never re-evaluates, so polling cannot extend ValidUntil,
/// bump the version or flip a Ready assessment. Re-evaluation is POST /{id}/re-evaluate.
/// </summary>
public class EvaluateResourceReadinessQueryHandler : IRequestHandler<EvaluateResourceReadinessQuery, ResourceReadinessDto>
{
    private readonly IPreMissionAssessmentService _service;

    public EvaluateResourceReadinessQueryHandler(IPreMissionAssessmentService service)
    {
        _service = service;
    }

    public async Task<ResourceReadinessDto> Handle(EvaluateResourceReadinessQuery request, CancellationToken cancellationToken)
    {
        var assessment = await _service.GetAsync(request.AssessmentId, cancellationToken);
        var dto = assessment.ToDto();

        var snapshots = assessment.PersonnelCandidates.Select(p => p.SnapshotAt)
            .Concat(assessment.DroneCandidates.Select(d => d.SnapshotAt))
            .ToList();
        var evaluatedAt = snapshots.Count > 0 ? snapshots.Max() : (assessment.UpdatedAt ?? assessment.CreatedAt);

        return new ResourceReadinessDto(
            dto.Id,
            dto.Status,
            dto.SiteFeasibilityStatus,
            dto.OverallTechnicalHealth,
            assessment.Status == PreMissionAssessmentStatus.Ready,
            dto.PersonnelCandidates,
            dto.DroneCandidates,
            evaluatedAt
        );
    }
}
