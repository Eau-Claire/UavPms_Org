using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Application.Features.AssetComponents.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Application.Common.Exceptions;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Queries.GetAssetComponentById;

public class GetAssetComponentByIdQueryHandler : IRequestHandler<GetAssetComponentByIdQuery, AssetComponentDetailDto>
{
    private readonly IAssetComponentRepository _assetRepository;

    public GetAssetComponentByIdQueryHandler(IAssetComponentRepository assetRepository)
    {
        _assetRepository = assetRepository;
    }

    public async Task<AssetComponentDetailDto> Handle(GetAssetComponentByIdQuery request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetAssetWithDetailsAsync(request.Id);
        if (asset == null || asset.IsDeleted)
        {
            throw new NotFoundException("AssetComponent", request.Id);
        }

        // Lọc các DetectedAnomalies ở trạng thái hoạt động (Confirmed và chưa được resolved)
        var activeAnomalies = asset.DetectedAnomalies
            .Where(da => da.ValidationStatus == "Confirmed")
            .Select(da => new ActiveAnomalyDto(
                da.Id,
                da.Category?.CategoryName ?? "Unknown",
                da.ConfidenceScore,
                da.ValidationStatus,
                da.CreatedAt
            ))
            .ToList();

        return new AssetComponentDetailDto(
            asset.Id,
            asset.TowerId,
            asset.Tower?.TowerCode ?? "Unknown",
            asset.ComponentType,
            asset.ComponentCode,
            asset.Status,
            asset.CurrentHealthScore,
            asset.RiskLevel,
            asset.LastInspectedAt,
            activeAnomalies
        );
    }
}
