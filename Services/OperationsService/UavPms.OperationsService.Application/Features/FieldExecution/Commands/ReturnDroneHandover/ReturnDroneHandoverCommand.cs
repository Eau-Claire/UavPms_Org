using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.FieldExecution.Commands.ReturnDroneHandover;

public record ReturnDroneHandoverCommand(
    Guid MissionId,
    Guid DroneId,
    string Condition,
    string? ReturnChecklistJson = null
) : IRequest<DroneHandoverResponseDto>;
