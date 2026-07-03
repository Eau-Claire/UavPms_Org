using MediatR;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.Missions.DTOs;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Missions.Queries.GetMissionDetails;

public class GetMissionDetailsQueryHandler : IRequestHandler<GetMissionDetailsQuery, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public GetMissionDetailsQueryHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }
    
    public async Task<MissionDto> Handle(GetMissionDetailsQuery request, CancellationToken cancellationToken)
    {
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
        };
    }
}