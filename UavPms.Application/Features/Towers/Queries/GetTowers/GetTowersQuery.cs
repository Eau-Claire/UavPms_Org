using MediatR;
using UavPms.Application.Features.Towers.DTOs;

namespace UavPms.Application.Features.Towers.Queries.GetTowers;

public record GetTowersQuery(
    int Page = 1,
    int PageSize = 10,
    Guid? LineAssetId = null
) : IRequest<PaginatedTowersResponse>;