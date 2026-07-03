using MediatR;
using UavPms.Application.Features.Missions.DTOs;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Application.Features.Missions.Queries.GetMyMissions;

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
            RouteData = mission.RouteData,
            AssignedToUserId = mission.AssignedToUserId,
            AssignedToUsername = mission.AssignedToUser?.Username ?? string.Empty,
            DroneCode = mission.DroneCode,
            Status = mission.Status,
            Description = mission.Description,
            ManagerId = mission.ManagerId,
            ManagerUsername = mission.Manager?.Username ?? string.Empty,
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt
        }).ToList();
    }
}