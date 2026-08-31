using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.CreateMission;

public record CreateMissionCommand(
    string Title,
    Guid RegionId,
    Guid InspectorId,
    Guid UavId,
    IReadOnlyList<Guid> TargetTowerIds,
    DateTime? ScheduledStartAt,
    string? Status,
    string? Description) : IRequest<MissionDto>;
