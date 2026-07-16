using MediatR;
using UavPms.Application.Common.DTOs;
using UavPms.Application.Features.Assets.DTOs;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Assets.Queries.GetAssets;

public class GetAssetsQueryHandler : IRequestHandler<GetAssetsQuery, PaginatedAssetsResponse>
{
    private readonly IAssetRepository _assetRepository;
    
    public GetAssetsQueryHandler(IAssetRepository assetRepository)
    {
        _assetRepository = assetRepository;
    }
    public async Task<PaginatedAssetsResponse> Handle(GetAssetsQuery request, CancellationToken cancellationToken)
    {
        var (assets, totalCount) = await _assetRepository.GetAssetsPagedAsync(
            request.Page,
            request.PageSize,
            request.TowerCode,
            request.AssetType,
            request.Status);
        
        var dtos = assets.Select(a => new AssetDto(
            a.Id,
            a.TowerId,
            a.AssetType,
            a.AssetCode,
            a.Status,
            a.CurrentHealthScore,
            a.RiskLevel,
            a.LastInspectedAt
        )).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        var pagination = new PaginationMetaData(request.Page, request.PageSize, totalCount, totalPages);
        
        return new PaginatedAssetsResponse(dtos, pagination);
    }
}