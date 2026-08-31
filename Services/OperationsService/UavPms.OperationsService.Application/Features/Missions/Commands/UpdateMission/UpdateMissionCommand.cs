using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.UpdateMission;

public record UpdateMissionCommand
(
    Guid Id,
    string Title,
    Guid InspectorId,
    Guid UavId,
    DateTime? ScheduledStartAt,
    string Status,
    string? Description) : IRequest<MissionDto>;
