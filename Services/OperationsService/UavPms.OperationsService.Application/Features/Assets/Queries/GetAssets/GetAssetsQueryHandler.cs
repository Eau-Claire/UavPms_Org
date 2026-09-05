using MediatR;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Application.Features.Assets.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Assets.Queries.GetAssets;

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
            request.TowerId,
            request.AssetType,
            request.Status,
            request.RiskLevels,
            request.MinHealthScore,
            request.MaxHealthScore,
            request.RegionId,
            request.LineId,
            request.SortBy,
            request.SortOrder);

        var defectCounts = await _assetRepository.GetConfirmedDefectCountsAsync(
            assets.Select(a => a.Id).ToList(),
            cancellationToken);
        
        var dtos = assets.Select(a => new AssetDto(
            a.Id,
            a.TowerId,
            a.AssetType,
            a.AssetCode,
            a.Status,
            a.CurrentHealthScore,
            a.RiskLevel,
            a.LastInspectedAt,
            defectCounts.GetValueOrDefault(a.Id),
            a.Tower?.TowerCode,
            a.Tower?.LineAssetId,
            a.Tower?.TransmissionLine?.LineName,
            a.Tower?.TransmissionLine?.Substation?.RegionAssetId,
            a.Tower?.TransmissionLine?.Substation?.Region?.RegionName
        )).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        var pagination = new PaginationMetaData(request.Page, request.PageSize, totalCount, totalPages);
        
        return new PaginatedAssetsResponse(dtos, pagination);
    }
}
