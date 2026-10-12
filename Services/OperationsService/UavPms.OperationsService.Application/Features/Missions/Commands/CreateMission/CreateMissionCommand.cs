using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Enums;

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
    IReadOnlyList<Guid>? TargetAssetIds = null,
    MissionPriority Priority = MissionPriority.Normal,
    InspectionObjective Objective = InspectionObjective.PeriodicInspection,
    IReadOnlyList<string>? PriorityDefects = null,
    string? EmergencyReason = null,
    bool IsImmediate = false) : IRequest<MissionDto>;
