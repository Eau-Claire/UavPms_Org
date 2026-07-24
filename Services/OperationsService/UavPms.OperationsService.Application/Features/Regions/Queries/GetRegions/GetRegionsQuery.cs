using MediatR;
using UavPms.OperationsService.Application.Features.Regions.DTOs;

namespace UavPms.OperationsService.Application.Features.Regions.Queries.GetRegions;

public record GetRegionsQuery(
    int Page = 1,
    int PageSize = 10,
    string? SearchTerm = null
) : IRequest<PaginatedRegionsResponse>;
