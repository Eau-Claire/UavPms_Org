using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.Missions.Queries.ListMissions;

public class ListMissionsQueryHandler : IRequestHandler<ListMissionsQuery, PaginatedMissionsResponse>
{
    private readonly IMissionRepository _missionRepository;
    private readonly ICurrentUserServices? _current;
    private readonly IGenericRepository<UserGeographicScope>? _scopeRepository;

    public ListMissionsQueryHandler(
        IMissionRepository missionRepository,
        ICurrentUserServices? current = null,
        IGenericRepository<UserGeographicScope>? scopeRepository = null)
    {
        _missionRepository = missionRepository;
        _current = current;
        _scopeRepository = scopeRepository;
    }

    public async Task<PaginatedMissionsResponse> Handle(ListMissionsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid?>? allowedRegionIds = null;
        if (_current is { IsAuthenticated: true } &&
            !_current.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase) &&
            _scopeRepository != null)
        {
            var scopes = await _scopeRepository.FindAsync(s => s.UserId == _current.UserId, track: false);
            allowedRegionIds = scopes.Select(s => (Guid?)s.RegionId).ToList();
        }

        var (items, totalCount) = await _missionRepository.GetMissionsPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.Status,
            request.SortBy,
            request.SortDescending,
            request.Priority,
            allowedRegionIds);

        var dtos = items.Select(mission => new MissionDto
        {
            Id = mission.Id,
            MissionCode = mission.MissionCode,
            Title = mission.Title,
            RouteData = string.Empty,
            AssignedToUserId = mission.InspectorId ?? Guid.Empty,
            AssignedToEmail = mission.Inspector?.Email ?? string.Empty,
            DroneCode = string.Empty,
            InspectorId = mission.InspectorId,
            InspectorEmail = mission.Inspector?.Email ?? string.Empty,
            UavId = mission.UavId,
            ScheduledStartAt = mission.ScheduledStartAt,
            PlannedStart = mission.PlannedStart,
            PlannedEnd = mission.PlannedEnd,
            Status = mission.Status.ToString(),
            Priority = mission.Priority.ToString(),
            Objective = mission.Objective.ToString(),
            PriorityDefects = !string.IsNullOrWhiteSpace(mission.PriorityDefectsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(mission.PriorityDefectsJson) ?? new List<string>()
                : new List<string>(),
            EmergencyReason = mission.EmergencyReason,
            IsImmediate = mission.IsImmediate,
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
