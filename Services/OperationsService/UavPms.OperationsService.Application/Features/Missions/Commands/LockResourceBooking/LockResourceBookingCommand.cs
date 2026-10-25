using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.LockResourceBooking;

public record LockResourceBookingCommand(
    Guid MissionId,
    Guid? UserId,
    Guid? DroneId,
    DateTime StartAt,
    DateTime EndAt
) : IRequest<ResourceBookingDto>;
