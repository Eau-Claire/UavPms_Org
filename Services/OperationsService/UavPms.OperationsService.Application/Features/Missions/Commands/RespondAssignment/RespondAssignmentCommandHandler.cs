using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.RespondAssignment;

public class RespondAssignmentCommandHandler : IRequestHandler<RespondAssignmentCommand, MissionAssignmentResponseDto>
{
    private readonly IMissionLifecycleService _lifecycle;

    public RespondAssignmentCommandHandler(IMissionLifecycleService lifecycle)
    {
        _lifecycle = lifecycle;
    }

    public async Task<MissionAssignmentResponseDto> Handle(RespondAssignmentCommand request, CancellationToken cancellationToken)
    {
        MissionAssignment assignment;
        if (request.Accept)
        {
            assignment = await _lifecycle.AcceptAssignmentAsync(request.MissionId, cancellationToken);
        }
        else
        {
            assignment = await _lifecycle.PostponeAssignmentAsync(request.MissionId, request.Reason ?? "Postponed", cancellationToken);
        }

        return MapAssignment(assignment);
    }

    private static MissionAssignmentResponseDto MapAssignment(MissionAssignment a) => new()
    {
        Id = a.Id,
        MissionId = a.MissionId,
        UserId = a.UserId,
        AssignmentRole = a.AssignmentRole,
        Status = a.Status.ToString(),
        ResponseStatus = a.ResponseStatus.ToString(),
        IsRequired = a.IsRequired,
        AssignedAt = a.AssignedAt,
        RespondedAt = a.RespondedAt,
        ResponseReason = a.ResponseReason,
        Version = a.Version,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt
    };
}
