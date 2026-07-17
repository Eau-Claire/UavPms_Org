using MediatR;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.Towers.DTOs;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Towers.Commands.UpdateTower;

public class UpdateTowerCommandHandler : IRequestHandler<UpdateTowerCommand, TowerDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITowerRepository _towerRepository;
    private readonly ITransmissionLineRepository _transmissionLineRepository;

    public UpdateTowerCommandHandler(
        ITowerRepository towerRepository,
        ITransmissionLineRepository transmissionLineRepository,
        IUnitOfWork unitOfWork)
    {
        _towerRepository = towerRepository;
        _transmissionLineRepository = transmissionLineRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<TowerDto> Handle(UpdateTowerCommand request, CancellationToken cancellationToken)
    {
        var tower = await _towerRepository.GetByIdAsync(request.Id);
        if (tower == null || tower.IsDeleted)
        {
            throw new NotFoundException("Tower",  request.Id);
        }
        
        var line = await _transmissionLineRepository.GetByIdAsync(request.LineAssetId);
        if (line == null || line.IsDeleted)
        {
            throw new NotFoundException("Line", request.LineAssetId);
        }

        Geometry? geom = null;
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        geom = geometryFactory.CreatePoint(new Coordinate(request.Longitude, request.Latitude));

        tower.LineAssetId = request.LineAssetId;
        tower.TowerCode = request.TowerCode;
        tower.Geom = geom;
        tower.UpdatedAt = DateTime.UtcNow;
        
        await _towerRepository.UpdateAsync(tower);
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