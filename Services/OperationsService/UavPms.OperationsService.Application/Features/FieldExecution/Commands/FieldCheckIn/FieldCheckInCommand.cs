using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.FieldExecution.Commands.FieldCheckIn;

public record FieldCheckInCommand(
    Guid MissionId,
    double Latitude,
    double Longitude
) : IRequest<MissionCheckInResponseDto>;
