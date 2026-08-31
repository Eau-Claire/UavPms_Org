using MediatR;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Application.Features.AssetComponents.DTOs;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Queries.GetAssetComponents;

public record GetAssetComponentsQuery(
    int Page = 1,
    int PageSize = 10,
    Guid? TowerCode = null,
    string? ComponentType = null,
    string? Status = null,
    IReadOnlyList<string>? RiskLevels = null,
    double? MinHealthScore = null,
    double? MaxHealthScore = null,
    Guid? RegionId = null,
    Guid? LineId = null,
    string? SortBy = null,
    string? SortOrder = null
) : IRequest<PaginatedAssetComponentsResponse>;
