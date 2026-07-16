using MediatR;
using UavPms.Application.Common.DTOs;
using UavPms.Application.Features.Assets.DTOs;

namespace UavPms.Application.Features.Assets.Queries.GetAssets;

public record GetAssetsQuery(
    int Page = 1,
    int PageSize = 10,
    Guid? TowerCode = null,
    string? AssetType = null,
    string? Status = null
) : IRequest<PaginatedAssetsResponse>;