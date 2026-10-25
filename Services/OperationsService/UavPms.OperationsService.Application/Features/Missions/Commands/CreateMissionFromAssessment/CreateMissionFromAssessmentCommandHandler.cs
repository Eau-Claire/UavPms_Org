using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.CreateMissionFromAssessment;

public class CreateMissionFromAssessmentCommandHandler : IRequestHandler<CreateMissionFromAssessmentCommand, MissionOperationResultDto>
{
    private readonly IPreMissionAssessmentService _service;

    public CreateMissionFromAssessmentCommandHandler(IPreMissionAssessmentService service)
    {
        _service = service;
    }

    public async Task<MissionOperationResultDto> Handle(CreateMissionFromAssessmentCommand command, CancellationToken cancellationToken)
    {
        var mission = await _service.CreateMissionFromAssessmentAsync(command.Request, cancellationToken);
        return MapMissionResult(mission);
    }

    private static MissionOperationResultDto MapMissionResult(Mission m) => new()
    {
        Id = m.Id,
        MissionCode = m.MissionCode,
        Title = m.Title,
        Status = m.Status.ToString(),
        Priority = m.Priority.ToString(),
        RegionId = m.RegionId,
        InspectorId = m.InspectorId,
        UavId = m.UavId,
        ManagerId = m.ManagerId,
        PlannedStart = m.PlannedStart,
        PlannedEnd = m.PlannedEnd,
        ConfirmationDeadline = m.ConfirmationDeadline,
        AcceptedAt = m.AcceptedAt,
        StartedAt = m.StartedAt,
        EndedAt = m.EndedAt,
        PostponedAt = m.PostponedAt,
        ManagerInstructions = m.ManagerInstructions,
        Version = m.Version,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
