using MediatR;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Application.Features.Assets.DTOs;

namespace UavPms.OperationsService.Application.Features.Assets.Queries.GetAssets;

public record GetAssetsQuery(
    int Page = 1,
    int PageSize = 10,
    Guid? TowerCode = null,
    string? AssetType = null,
    string? Status = null
) : IRequest<PaginatedAssetsResponse>;