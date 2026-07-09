using MediatR;
using UavPms.Application.Features.Missions.DTOs;

namespace UavPms.Application.Features.Missions.Commands.UpdateMission;

public record UpdateMissionCommand
(
    Guid Id,
    string Title,
    string RouteData,
    Guid AssignedToUserId,
    string DroneCode,
    string Status,
    string? Description) : IRequest<MissionDto>;