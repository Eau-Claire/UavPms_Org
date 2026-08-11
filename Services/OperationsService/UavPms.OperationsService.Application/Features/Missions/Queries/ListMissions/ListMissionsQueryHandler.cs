using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Missions.Queries.ListMissions;

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
            request.Status,
            request.SortBy,
            request.SortDescending);

        var dtos = items.Select(mission => new MissionDto
        {
            Id = mission.Id,
            MissionCode = mission.MissionCode,
            Title = mission.Title,
            RouteData = mission.RouteData,
            AssignedToUserId = mission.AssignedToUserId,
            AssignedToEmail = mission.AssignedToUser?.Email ?? string.Empty,
            DroneCode = mission.DroneCode,
            Status = mission.Status.ToString(),
            Description = mission.Description,
            ManagerId = mission.ManagerId,
            ManagerEmail = mission.Manager?.Email ?? string.Empty,
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt
        }).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        var metaData = new PaginationMetaData(request.Page, request.PageSize, totalCount, totalPages);

        return new PaginatedMissionsResponse(dtos, metaData);
    }
}
