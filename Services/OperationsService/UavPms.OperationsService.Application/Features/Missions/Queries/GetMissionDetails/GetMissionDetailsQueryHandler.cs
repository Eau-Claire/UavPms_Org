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
            RouteData = mission.RouteData,
            AssignedToUserId = mission.AssignedToUserId,
            AssignedToEmail = mission.AssignedToUser?.Email ?? string.Empty,
            DroneCode = mission.DroneCode,
            InspectorId = mission.InspectorId,
            InspectorEmail = mission.Inspector?.Email ?? mission.AssignedToUser?.Email ?? string.Empty,
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
                    AssetId = target.AssetId,
                    AssetCode = target.Asset?.AssetCode ?? string.Empty,
                    Sequence = target.Sequence,
                    InspectionStatus = target.InspectionStatus,
                    Latitude = target.Asset?.Tower?.Geom?.Coordinate.Y,
                    Longitude = target.Asset?.Tower?.Geom?.Coordinate.X
                })
                .ToList(),
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt
        };
    }
}
