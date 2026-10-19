using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments.Commands.ConfirmAssessment;

public class ConfirmAssessmentCommandHandler : IRequestHandler<ConfirmAssessmentCommand, PreMissionAssessmentDto>
{
    private readonly IPreMissionAssessmentService _service;

    public ConfirmAssessmentCommandHandler(IPreMissionAssessmentService service)
    {
        _service = service;
    }

    public async Task<PreMissionAssessmentDto> Handle(ConfirmAssessmentCommand request, CancellationToken cancellationToken)
    {
        var assessment = await _service.GetAsync(request.AssessmentId, cancellationToken);
        if (assessment.Status == PreMissionAssessmentStatus.Evaluating)
        {
            assessment = await _service.EvaluateAsync(request.AssessmentId, cancellationToken);
        }

        if (assessment.Status != PreMissionAssessmentStatus.Ready)
        {
            throw new BusinessRuleException("ASSESSMENT_NOT_READY", "Đánh giá chưa thỏa mãn điều kiện sẵn sàng để tạo nhiệm vụ.");
        }

        return assessment.ToDto();
    }
}
