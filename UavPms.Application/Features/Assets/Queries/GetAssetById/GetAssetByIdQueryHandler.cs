using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UavPms.Application.Features.Assets.DTOs;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Application.Common.Exceptions;

namespace UavPms.Application.Features.Assets.Queries.GetAssetById;

public class GetAssetByIdQueryHandler : IRequestHandler<GetAssetByIdQuery, AssetDetailDto>
{
    private readonly IAssetRepository _assetRepository;

    public GetAssetByIdQueryHandler(IAssetRepository assetRepository)
    {
        _assetRepository = assetRepository;
    }

    public async Task<AssetDetailDto> Handle(GetAssetByIdQuery request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetAssetWithDetailsAsync(request.Id);
        if (asset == null || asset.IsDeleted)
        {
            throw new NotFoundException("Asset", request.Id);
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

        return new AssetDetailDto(
            asset.Id,
            asset.TowerId,
            asset.Tower?.TowerCode ?? "Unknown",
            asset.AssetType,
            asset.AssetCode,
            asset.Status,
            asset.CurrentHealthScore,
            asset.RiskLevel,
            asset.LastInspectedAt,
            activeAnomalies
        );
    }
}