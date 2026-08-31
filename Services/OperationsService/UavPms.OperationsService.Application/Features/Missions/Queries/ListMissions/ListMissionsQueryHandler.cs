using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Missions.Queries.ListMissions;

public class ListMissionsQueryHandler : IRequestHandler<ListMissionsQuery, PaginatedMissionsResponse>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IUserRegionAssignmentRepository _assignmentRepository;
    private readonly ICurrentUserServices _currentUser;

    public ListMissionsQueryHandler(IMissionRepository missionRepository, IUserRegionAssignmentRepository assignmentRepository, ICurrentUserServices currentUser)
    {
        _missionRepository = missionRepository;
        _assignmentRepository = assignmentRepository;
        _currentUser = currentUser;
    }

    public async Task<PaginatedMissionsResponse> Handle(ListMissionsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<Guid>? regionIds = null;
        if (_currentUser.Roles?.Contains("SystemAdmin", StringComparer.OrdinalIgnoreCase) != true)
            regionIds = (await _assignmentRepository.GetRegionIdsAsync(_currentUser.UserId, cancellationToken)).ToArray();

        var (items, totalCount) = await _missionRepository.GetMissionsPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.Status,
            request.SortBy,
            request.SortDescending,
            regionIds);

        var dtos = items.Select(mission => new MissionDto
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

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        var metaData = new PaginationMetaData(request.Page, request.PageSize, totalCount, totalPages);

        return new PaginatedMissionsResponse(dtos, metaData);
    }
}
