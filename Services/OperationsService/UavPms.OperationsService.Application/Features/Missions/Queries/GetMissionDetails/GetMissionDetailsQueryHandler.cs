using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Missions.Queries.GetMissionDetails;

public class GetMissionDetailsQueryHandler : IRequestHandler<GetMissionDetailsQuery, MissionDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly ICurrentUserServices? _current;

    public GetMissionDetailsQueryHandler(IMissionRepository missionRepository, ICurrentUserServices? current = null)
    {
        _missionRepository = missionRepository;
        _current = current;
    }
    
    public async Task<MissionDto> Handle(GetMissionDetailsQuery request, CancellationToken cancellationToken)
    {
        if (_current is { IsAuthenticated: true } && !await _missionRepository.UserCanAccessAsync(request.Id, _current.UserId,
                _current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase), cancellationToken))
            throw new ForbiddenException("MISSION_ACCESS_DENIED");
        var mission = await _missionRepository.GetMissionDetailsByIdAsync(request.Id);
        if (mission  == null)
        {
            throw new NotFoundException("Mission", request.Id);
        }

        return new MissionDto
        {
            Id = mission.Id,
            MissionCode = mission.MissionCode,
            Title = mission.Title,
            RouteData = string.Empty,
            AssignedToUserId = mission.PrimaryInspectorId() ?? Guid.Empty,
            AssignedToEmail = mission.PrimaryInspector()?.Email ?? string.Empty,
            DroneCode = mission.Uav?.UavCode ?? string.Empty,
            InspectorId = mission.PrimaryInspectorId(),
            InspectorEmail = mission.PrimaryInspector()?.Email ?? string.Empty,
            UavId = mission.UavId,
            ScheduledStartAt = mission.PlannedStart,
            RegionId = mission.RegionId,
            RegionName = mission.Region?.Name,
            ScheduleId = mission.ScheduleId,
            ScheduleName = mission.Schedule?.Name,
            MissionType = mission.MissionType.ToString(),
            TriggerReason = mission.TriggerReason,
            PlannedStart = mission.PlannedStart,
            PlannedEnd = mission.PlannedEnd,
            ActualStart = mission.StartedAt,
            ActualCompleted = mission.EndedAt,
            BoundaryWkt = mission.Boundary?.AsText(),
            Team = mission.Assignments.Select(a => new MissionAssignmentDto(a.Id, a.UserId, a.User?.FullName ?? "", a.AssignmentRole,
                a.Status.ToString(), a.ResponseStatus.ToString(), mission.CheckIns.Where(c => c.UserId == a.UserId && c.Status == MissionCheckInStatus.CheckedIn).Select(c => (DateTime?)c.CheckedInAt).FirstOrDefault())).ToList(),
            Status = mission.Status.ToString(),
            Priority = mission.Priority.ToString(),
            Objective = mission.Objective.ToString(),
            PriorityDefects = mission.PriorityDefectCodes(),
            EmergencyReason = mission.TriggerReason,
            IsImmediate = mission.IsImmediate,
            Description = mission.Description,
            ManagerId = mission.ManagerId,
            ManagerEmail = mission.Manager?.Email ?? string.Empty,
            Targets = mission.MissionTargets
                .OrderBy(target => target.Sequence)
                .Select(target => new MissionTargetDto
                {
                    TowerId = target.TowerId,
                    TowerCode = target.Tower?.TowerCode ?? string.Empty,
                    Sequence = target.Sequence,
                    InspectionStatus = target.InspectionStatus.ToString(),
                    Latitude = target.Tower?.Geom?.Coordinate.Y,
                    Longitude = target.Tower?.Geom?.Coordinate.X,
                    RegionId = target.Tower?.RegionId
                })
                .ToList(),
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt
        };
    }
}
