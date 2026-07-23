using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.Missions.Queries.ListMissions;

public record ListMissionsQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Status) : IRequest<PaginatedMissionsResponse>;