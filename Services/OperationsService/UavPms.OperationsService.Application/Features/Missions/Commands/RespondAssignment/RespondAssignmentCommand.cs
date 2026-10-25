using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.RespondAssignment;

public record RespondAssignmentCommand(
    Guid MissionId,
    bool Accept,
    string? Reason = null
) : IRequest<MissionAssignmentResponseDto>;
