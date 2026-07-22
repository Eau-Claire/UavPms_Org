using MediatR;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.Towers.DTOs;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Towers.Queries.GetTowerById;

public class GetTowerByIQueryHandler : IRequestHandler<GetTowerByIdQuery, TowerDto>
{
    
    private readonly ITowerRepository _towerRepository;

    public GetTowerByIQueryHandler(ITowerRepository towerRepository)
    {
        _towerRepository = towerRepository;
    }
    
    public async Task<TowerDto> Handle(GetTowerByIdQuery request, CancellationToken cancellationToken)
    {
        var tower = await _towerRepository.GetByIdAsync(request.Id);
        if (tower == null || tower.IsDeleted)
        {
            throw new NotFoundException("Tower", request.Id);
        }

        return new TowerDto(
            tower.Id,
            tower.LineAssetId,
            tower.TowerCode,
            tower.Geom != null ? tower.Geom.Coordinate.Y : 0.0,
            tower.Geom != null ? tower.Geom.Coordinate.X : 0.0
        );
    }
}