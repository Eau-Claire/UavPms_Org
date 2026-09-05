using MediatR;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Application.Features.Assets.DTOs;

namespace UavPms.OperationsService.Application.Features.Assets.Queries.GetAssets;

public record GetAssetsQuery(
    int Page = 1,
    int PageSize = 10,
    Guid? TowerId = null,
    string? AssetType = null,
    string? Status = null,
    IReadOnlyList<string>? RiskLevels = null,
    double? MinHealthScore = null,
    double? MaxHealthScore = null,
    Guid? RegionId = null,
    Guid? LineId = null,
    string? SortBy = null,
    string? SortOrder = null
) : IRequest<PaginatedAssetsResponse>;
