using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Missions.Queries.GetMissionDetails;

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
            RouteData = string.Empty,
            AssignedToUserId = mission.InspectorId,
            AssignedToEmail = mission.Inspector?.Email ?? string.Empty,
            DroneCode = mission.Uav?.UavCode ?? string.Empty,
            InspectorId = mission.InspectorId,
            InspectorEmail = mission.Inspector?.Email ?? string.Empty,
            UavId = mission.UavId,
            ScheduledStartAt = mission.ScheduledStartAt,
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
