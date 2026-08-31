using MediatR;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Application.Features.AssetComponents.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Queries.GetAssetComponents;

public class GetAssetComponentsQueryHandler : IRequestHandler<GetAssetComponentsQuery, PaginatedAssetComponentsResponse>
{
    private readonly IAssetComponentRepository _assetRepository;
    
    public GetAssetComponentsQueryHandler(IAssetComponentRepository assetRepository)
    {
        _assetRepository = assetRepository;
    }
    public async Task<PaginatedAssetComponentsResponse> Handle(GetAssetComponentsQuery request, CancellationToken cancellationToken)
    {
        var (assets, totalCount) = await _assetRepository.GetAssetComponentsPagedAsync(
            request.Page,
            request.PageSize,
            request.TowerCode,
            request.ComponentType,
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
        
        var dtos = assets.Select(a => new AssetComponentDto(
            a.Id,
            a.TowerId,
            a.ComponentType,
            a.ComponentCode,
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
        
        return new PaginatedAssetComponentsResponse(dtos, pagination);
    }
}
