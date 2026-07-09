using MediatR;
using UavPms.Application.Features.Missions.DTOs;

namespace UavPms.Application.Features.Missions.Queries.ListMissions;

public record ListMissionsQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Status) : IRequest<PaginatedMissionsResponse>;