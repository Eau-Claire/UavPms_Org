using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using UavPms.Application.Features.Assets.DTOs;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Application.Common.Exceptions;

namespace UavPms.Application.Features.Assets.Commands.UpdateAsset;

public class UpdateAssetCommandHandler : IRequestHandler<UpdateAssetCommand, AssetDto>
{
    private readonly IAssetRepository _assetRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAssetCommandHandler(
        IAssetRepository assetRepository, 
        ITowerRepository towerRepository, 
        IUnitOfWork unitOfWork)
    {
        _assetRepository = assetRepository;
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AssetDto> Handle(UpdateAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id);
        if (asset == null || asset.IsDeleted)
        {
            throw new NotFoundException("Asset", request.Id);
        }

        var tower = await _towerRepository.GetByIdAsync(request.TowerId);
        if (tower == null || tower.IsDeleted)
        {
            throw new NotFoundException("Tower", request.TowerId);
        }

        asset.TowerId = request.TowerId;
        asset.AssetType = request.AssetType;
        asset.AssetCode = request.AssetCode;
        asset.Status = request.Status;
        asset.CurrentHealthScore = request.CurrentHealthScore;
        asset.RiskLevel = request.RiskLevel;
        asset.UpdatedAt = DateTime.UtcNow;

        await _assetRepository.UpdateAsync(asset);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssetDto(
            asset.Id,
            asset.TowerId,
            asset.AssetType,
            asset.AssetCode,
            asset.Status,
            asset.CurrentHealthScore,
            asset.RiskLevel,
            asset.LastInspectedAt
        );
    }
}
