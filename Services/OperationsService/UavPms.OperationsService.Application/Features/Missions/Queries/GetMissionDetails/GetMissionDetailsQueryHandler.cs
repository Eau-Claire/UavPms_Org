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
            AssignedToUserId = mission.InspectorId,
            AssignedToEmail = mission.Inspector?.Email ?? string.Empty,
            DroneCode = mission.Uav?.UavCode ?? string.Empty,
            InspectorId = mission.InspectorId,
            InspectorEmail = mission.Inspector?.Email ?? string.Empty,
            UavId = mission.UavId,
            ScheduledStartAt = mission.ScheduledStartAt,
            RegionId = mission.RegionId == Guid.Empty ? null : mission.RegionId,
            RegionName = mission.Region?.RegionName,
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
                a.Status.ToString(), mission.CheckIns.Where(c => c.UserId == a.UserId && c.Status == MissionCheckInStatus.CheckedIn).Select(c => (DateTime?)c.CheckedInAt).FirstOrDefault())).ToList(),
            Status = mission.Status.ToString(),
            Description = mission.Description,
            ManagerId = mission.ManagerId,
            ManagerEmail = mission.Manager?.Email ?? string.Empty,
            Targets = mission.MissionTargets
                .OrderBy(target => target.Sequence)
                .Select(target => new MissionTargetDto
                {
                    TowerId = target.Asset?.TowerId ?? Guid.Empty,
                    TowerCode = target.Asset?.Tower?.TowerCode ?? string.Empty,
                    AssetId = target.AssetId,
                    AssetCode = target.Asset?.AssetCode,
                    AssetType = target.Asset?.AssetType,
                    Sequence = target.Sequence,
                    InspectionStatus = target.InspectionStatus.ToString(),
                    Latitude = target.Asset?.Location?.Y,
                    Longitude = target.Asset?.Location?.X,
                    PowerLineId = target.Asset?.PowerLineId,
                    PowerLineCode = target.Asset?.PowerLine?.Code,
                    PowerLineName = target.Asset?.PowerLine?.LineName
                })
                .ToList(),
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt
        };
    }
}
