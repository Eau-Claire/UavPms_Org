using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using UavPms.Application.Features.Assets.DTOs;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Application.Common.Exceptions;

namespace UavPms.Application.Features.Assets.Commands.CreateAsset;

public class CreateAssetCommandHandler : IRequestHandler<CreateAssetCommand, AssetDto>
{
    private readonly IAssetRepository _assetRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAssetCommandHandler(
        IAssetRepository assetRepository, 
        ITowerRepository towerRepository, 
        IUnitOfWork unitOfWork)
    {
        _assetRepository = assetRepository;
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AssetDto> Handle(CreateAssetCommand request, CancellationToken cancellationToken)
    {
        var tower = await _towerRepository.GetByIdAsync(request.TowerId);
        if (tower == null || tower.IsDeleted)
        {
            throw new NotFoundException("Tower", request.TowerId);
        }

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            TowerId = request.TowerId,
            AssetType = request.AssetType,
            AssetCode = request.AssetCode,
            Status = "Operational",
            CurrentHealthScore = 100.0,
            RiskLevel = "Low Risk",
            CreatedAt = DateTime.UtcNow
        };

        await _assetRepository.AddAsync(asset);
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
