using MediatR;
using UavPms.Application.Features.Missions.DTOs;
using UavPms.Application.Common.DTOs;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Missions.Queries.ListMissions;

public class ListMissionsQueryHandler : IRequestHandler<ListMissionsQuery, PaginatedMissionsResponse>
{
    private readonly IMissionRepository _missionRepository;

    public ListMissionsQueryHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<PaginatedMissionsResponse> Handle(ListMissionsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _missionRepository.GetMissionsPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.Status);

        var dtos = items.Select(mission => new MissionDto
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

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        var metaData = new PaginationMetaData(request.Page, request.PageSize, totalCount, totalPages);

        return new PaginatedMissionsResponse(dtos, metaData);
    }
}