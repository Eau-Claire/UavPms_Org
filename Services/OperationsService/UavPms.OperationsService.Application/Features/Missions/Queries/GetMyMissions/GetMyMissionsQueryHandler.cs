using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
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
            AssignedToUserId = mission.AssignedToUserId != Guid.Empty ? mission.AssignedToUserId : (mission.InspectorId != Guid.Empty ? mission.InspectorId : currentUserId),
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
            Status = mission.Status.ToString(),
            Description = mission.Description,
            ManagerId = mission.ManagerId != Guid.Empty ? mission.ManagerId : (Guid?)null,
            ManagerEmail = mission.Manager?.Email ?? string.Empty,
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt
        }).ToList();
    }
}
