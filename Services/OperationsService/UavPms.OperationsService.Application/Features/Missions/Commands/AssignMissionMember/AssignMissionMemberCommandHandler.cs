using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.AssignMissionMember;

public class AssignMissionMemberCommandHandler : IRequestHandler<AssignMissionMemberCommand, MissionAssignmentResponseDto>
{
    private readonly IMissionLifecycleService _lifecycle;

    public AssignMissionMemberCommandHandler(IMissionLifecycleService lifecycle)
    {
        _lifecycle = lifecycle;
    }

    public async Task<MissionAssignmentResponseDto> Handle(AssignMissionMemberCommand request, CancellationToken cancellationToken)
    {
        var assignment = await _lifecycle.AssignAsync(
            request.MissionId,
            new Mf01Assignment(request.UserId, request.AssignmentRole),
            cancellationToken);

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
