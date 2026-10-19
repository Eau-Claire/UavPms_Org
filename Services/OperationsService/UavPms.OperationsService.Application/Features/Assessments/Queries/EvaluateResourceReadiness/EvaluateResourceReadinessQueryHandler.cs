using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments.Queries.EvaluateResourceReadiness;

public class EvaluateResourceReadinessQueryHandler : IRequestHandler<EvaluateResourceReadinessQuery, ResourceReadinessDto>
{
    private readonly IPreMissionAssessmentService _service;

    public EvaluateResourceReadinessQueryHandler(IPreMissionAssessmentService service)
    {
        _service = service;
    }

    public async Task<ResourceReadinessDto> Handle(EvaluateResourceReadinessQuery request, CancellationToken cancellationToken)
    {
        var assessment = await _service.EvaluateAsync(request.AssessmentId, cancellationToken);
        var dto = assessment.ToDto();

        return new ResourceReadinessDto(
            dto.Id,
            dto.Status,
            dto.SiteFeasibilityStatus,
            dto.OverallTechnicalHealth,
            assessment.Status == PreMissionAssessmentStatus.Ready,
            dto.PersonnelCandidates,
            dto.DroneCandidates,
            DateTime.UtcNow
        );
    }
}
