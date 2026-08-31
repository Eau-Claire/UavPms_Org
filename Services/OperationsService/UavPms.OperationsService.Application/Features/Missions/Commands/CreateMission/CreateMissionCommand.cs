using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.CreateMission;

public record CreateMissionCommand(
    string Title,
    string? RouteData,
    Guid AssignedToUserId,
    string? DroneCode,
    string? Status,
    string? Description,
    DateTime? ScheduledStartAt = null,
    Guid? InspectorId = null,
    Guid? UavId = null,
    IReadOnlyList<Guid>? TargetAssetIds = null) : IRequest<MissionDto>;
