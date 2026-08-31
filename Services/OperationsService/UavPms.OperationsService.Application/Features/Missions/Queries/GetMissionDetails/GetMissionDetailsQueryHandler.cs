using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Missions.Queries.GetMissionDetails;

public class GetMissionDetailsQueryHandler : IRequestHandler<GetMissionDetailsQuery, MissionDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IUserRegionAssignmentRepository _assignments;
    private readonly ICurrentUserServices _currentUser;

    public GetMissionDetailsQueryHandler(IMissionRepository missionRepository, IUserRegionAssignmentRepository assignments, ICurrentUserServices currentUser)
    {
        _missionRepository = missionRepository;
        _assignments = assignments;
        _currentUser = currentUser;
    }
    
    public async Task<MissionDto> Handle(GetMissionDetailsQuery request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetMissionDetailsByIdAsync(request.Id);
        if (mission  == null)
        {
            throw new NotFoundException("Mission", request.Id);
        }
        if (_currentUser.Roles?.Contains("SystemAdmin", StringComparer.OrdinalIgnoreCase) != true &&
            !await _assignments.ExistsAsync(_currentUser.UserId, mission.RegionId, cancellationToken))
            throw new BusinessRuleException("Requesting user is not assigned to the mission Region.");

        return new MissionDto
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
        };
    }
}
