using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.FieldExecution.Commands.DeliverDroneHandover;

public record DeliverDroneHandoverCommand(
    Guid MissionId,
    Guid DroneId,
    Guid ReceivedBy,
    string Condition,
    string? InitialChecklistJson = null,
    bool Accepted = true
) : IRequest<DroneHandoverResponseDto>;
