using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Missions.Queries.GetMyMissions;

public class GetMyMissionsQueryHandler : IRequestHandler<GetMyMissionsQuery, List<MissionDto>>
{
    private readonly IMissionRepository _missionRepository;
    private readonly ICurrentUserServices _currentUserServices;

    public GetMyMissionsQueryHandler(
        IMissionRepository missionRepository,
        ICurrentUserServices currentUserServices)
    {
        _missionRepository = missionRepository;
        _currentUserServices = currentUserServices;
    }
    
    public async Task<List<MissionDto>> Handle(GetMyMissionsQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserServices.UserId;
        if (currentUserId == Guid.Empty)
        {
            return new List<MissionDto>();
        }

        var items = await _missionRepository.GetMissionsByAssignedUserAsync(currentUserId);
        
        return items.Select(mission => new MissionDto
        {
            Id = mission.Id,
            MissionCode = mission.MissionCode,
            Title = mission.Title,
            RouteData = mission.RouteData ?? string.Empty,
            AssignedToUserId = mission.AssignedToUserId != Guid.Empty ? mission.AssignedToUserId : ((mission.InspectorId.HasValue && mission.InspectorId.Value != Guid.Empty) ? mission.InspectorId.Value : currentUserId),
            AssignedToEmail = mission.Inspector?.Email ?? mission.AssignedToUser?.Email ?? string.Empty,
            AssignedToUsername = !string.IsNullOrWhiteSpace(mission.Inspector?.FullName) ? mission.Inspector.FullName : (mission.AssignedToUser?.FullName ?? mission.Inspector?.Email ?? string.Empty),
            DroneId = mission.UavId != Guid.Empty ? mission.UavId : (Guid?)null,
            DroneCode = mission.Uav?.UavCode ?? mission.DroneCode ?? string.Empty,
            InspectorId = mission.InspectorId != Guid.Empty ? mission.InspectorId : (Guid?)null,
            InspectorEmail = mission.Inspector?.Email ?? string.Empty,
            UavId = mission.UavId != Guid.Empty ? mission.UavId : (Guid?)null,
            ScheduledStartAt = mission.ScheduledStartAt ?? mission.PlannedStart,
            PlannedStart = mission.PlannedStart,
            PlannedEnd = mission.PlannedEnd,
            ConfirmationDeadline = mission.ConfirmationDeadline,
            ManagerInstructions = mission.ManagerInstructions,
            RegionId = mission.RegionId,
            RegionName = mission.Region?.RegionName,
            MissionType = mission.MissionType.ToString(),
            TriggerReason = mission.TriggerReason,
            BoundaryWkt = mission.Boundary?.AsText(),
            Team = mission.Assignments.Where(a => a.Status == MissionAssignmentStatus.Active).Select(a => new MissionAssignmentDto(
                a.Id,
                a.UserId,
                a.User?.FullName ?? a.User?.Email ?? string.Empty,
                a.AssignmentRole,
                a.Status.ToString(),
                a.ResponseStatus.ToString(),
                null)).ToList(),
            Status = mission.Status.ToString(),
            Priority = mission.Priority.ToString(),
            Objective = mission.Objective.ToString(),
            PriorityDefects = !string.IsNullOrWhiteSpace(mission.PriorityDefectsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(mission.PriorityDefectsJson) ?? new List<string>()
                : new List<string>(),
            EmergencyReason = mission.EmergencyReason,
            IsImmediate = mission.IsImmediate,
            Description = mission.Description,
            ManagerId = mission.ManagerId != Guid.Empty ? mission.ManagerId : (Guid?)null,
            ManagerEmail = mission.Manager?.Email ?? string.Empty,
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt
        }).ToList();
    }
}
