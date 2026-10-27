using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;

namespace UavPms.OperationsService.Application.Features.Assessments.Commands.CreatePreMissionAssessment;

public class CreatePreMissionAssessmentCommandHandler : IRequestHandler<CreatePreMissionAssessmentCommand, PreMissionAssessmentDto>
{
    private readonly IPreMissionAssessmentService _service;

    public CreatePreMissionAssessmentCommandHandler(IPreMissionAssessmentService service)
    {
        _service = service;
    }

    public async Task<PreMissionAssessmentDto> Handle(CreatePreMissionAssessmentCommand request, CancellationToken cancellationToken)
    {
        var assessment = await _service.CreateAsync(
            request.RegionId,
            request.PlannedStart,
            request.PlannedEnd,
            request.TowerIds,
            request.BoundaryWkt,
            request.IdempotencyKey,
            cancellationToken);

        return assessment.ToDto();
    }
}
