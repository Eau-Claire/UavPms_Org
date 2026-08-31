using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Application.Features.AssetComponents.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Application.Common.Exceptions;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Commands.UpdateAssetComponent;

public class UpdateAssetComponentCommandHandler : IRequestHandler<UpdateAssetComponentCommand, AssetComponentDto>
{
    private readonly IAssetComponentRepository _assetRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAssetComponentCommandHandler(
        IAssetComponentRepository assetRepository,
        ITowerRepository towerRepository, 
        IUnitOfWork unitOfWork)
    {
        _assetRepository = assetRepository;
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AssetComponentDto> Handle(UpdateAssetComponentCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id);
        if (asset == null || asset.IsDeleted)
        {
            throw new NotFoundException("AssetComponent", request.Id);
        }

        var tower = await _towerRepository.GetByIdAsync(request.TowerId);
        if (tower == null || tower.IsDeleted)
        {
            throw new NotFoundException("Tower", request.TowerId);
        }

        asset.TowerId = request.TowerId;
        asset.ComponentType = request.ComponentType;
        asset.ComponentCode = request.ComponentCode;
        asset.Status = request.Status;
        asset.UpdatedAt = DateTime.UtcNow;

        await _assetRepository.UpdateAsync(asset);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssetComponentDto(
            asset.Id,
            asset.TowerId,
            asset.ComponentType,
            asset.ComponentCode,
            asset.Status,
            asset.CurrentHealthScore,
            asset.RiskLevel,
            asset.LastInspectedAt,
            asset.DetectedAnomalies.Count(d => d.ValidationStatus == "Confirmed"),
            tower.TowerCode,
            tower.LineAssetId,
            tower.TransmissionLine?.LineName,
            tower.TransmissionLine?.Substation?.RegionAssetId,
            tower.TransmissionLine?.Substation?.Region?.RegionName
        );
    }
}
