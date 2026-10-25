using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.AssignMissionMember;

public record AssignMissionMemberCommand(
    Guid MissionId,
    Guid UserId,
    string AssignmentRole
) : IRequest<MissionAssignmentResponseDto>;
