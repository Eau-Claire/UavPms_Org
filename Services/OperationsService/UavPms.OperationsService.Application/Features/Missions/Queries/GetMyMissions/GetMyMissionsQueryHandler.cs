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
            RegionId = mission.RegionId,
            InspectorId = mission.InspectorId,
            InspectorEmail = mission.Inspector?.Email ?? string.Empty,
            UavId = mission.UavId,
            TargetTowerIds = (mission.MissionTargets ?? new List<MissionTarget>()).OrderBy(x => x.Sequence).Select(x => x.TowerId).ToArray(),
            Status = mission.Status.ToString(),
            Description = mission.Description,
            ManagerId = mission.ManagerId,
            ManagerEmail = mission.Manager?.Email ?? string.Empty,
            ScheduledStartAt = mission.ScheduledStartAt,
            StartedAt = mission.StartedAt,
            EndedAt = mission.EndedAt,
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt
        }).ToList();
    }
}
