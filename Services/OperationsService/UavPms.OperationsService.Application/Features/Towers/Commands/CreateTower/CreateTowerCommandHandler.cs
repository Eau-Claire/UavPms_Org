using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Utilities;
using UavPms.OperationsService.Application.Features.Towers.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Towers.Commands.CreateTower;

public class CreateTowerCommandHandler : IRequestHandler<CreateTowerCommand, TowerDto>
{
    private readonly ITransmissionLineRepository _transmissionLineRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTowerCommandHandler(
        ITransmissionLineRepository transmissionLineRepository,
        ITowerRepository towerRepository,
        IUnitOfWork unitOfWork)
    {
        _transmissionLineRepository = transmissionLineRepository;
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<TowerDto> Handle(CreateTowerCommand request, CancellationToken cancellationToken)
    {
        var line = await _transmissionLineRepository.GetByIdAsync(request.LineAssetId);
        if (line == null || line.IsDeleted)
        {
            throw new NotFoundException("Line", request.LineAssetId);
        }

        var location = SpatialGeometryFactory.CreatePoint(request.Longitude, request.Latitude);

        var tower = new Tower
        {
            Id = Guid.NewGuid(),
            LineAssetId = request.LineAssetId,
            TowerCode = request.TowerCode,
            Geom = location,
            CreatedAt = DateTime.UtcNow,
        };
        
        await _towerRepository.AddAsync(tower);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TowerDto(
            tower.Id,
            tower.LineAssetId,
            tower.TowerCode,
            request.Latitude,
            request.Longitude
        );
    }
}
