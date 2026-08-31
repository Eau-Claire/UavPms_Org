using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Application.Features.AssetComponents.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Application.Common.Exceptions;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Commands.CreateAssetComponent;

public class CreateAssetComponentCommandHandler : IRequestHandler<CreateAssetComponentCommand, AssetComponentDto>
{
    private readonly IAssetComponentRepository _assetRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAssetComponentCommandHandler(
        IAssetComponentRepository assetRepository,
        ITowerRepository towerRepository, 
        IUnitOfWork unitOfWork)
    {
        _assetRepository = assetRepository;
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AssetComponentDto> Handle(CreateAssetComponentCommand request, CancellationToken cancellationToken)
    {
        var tower = await _towerRepository.GetByIdAsync(request.TowerId);
        if (tower == null || tower.IsDeleted)
        {
            throw new NotFoundException("Tower", request.TowerId);
        }

        var asset = new AssetComponent
        {
            Id = Guid.NewGuid(),
            TowerId = request.TowerId,
            ComponentType = request.ComponentType,
            ComponentCode = request.ComponentCode,
            Status = "Operational",
            CurrentHealthScore = 100,
            RiskLevel = "Low Risk",
            CreatedAt = DateTime.UtcNow
        };

        await _assetRepository.AddAsync(asset);
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
            0,
            tower.TowerCode,
            tower.LineAssetId,
            tower.TransmissionLine?.LineName,
            tower.TransmissionLine?.Substation?.RegionAssetId,
            tower.TransmissionLine?.Substation?.Region?.RegionName
        );
    }
}
