using MediatR;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.Towers.DTOs;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Towers.Commands.CreateTower;

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

        Geometry? geom = null;
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        geom = geometryFactory.CreatePoint(new Coordinate(request.Longitude, request.Latitude));

        var tower = new Tower
        {
            Id = Guid.NewGuid(),
            LineAssetId = request.LineAssetId,
            TowerCode = request.TowerCode,
            Geom = geom,
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