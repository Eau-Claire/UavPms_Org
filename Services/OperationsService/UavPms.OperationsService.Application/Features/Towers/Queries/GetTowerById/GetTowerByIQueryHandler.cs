using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Towers.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Towers.Queries.GetTowerById;

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
            tower.Geom != null ? tower.Geom.Y : 0.0,
            tower.Geom != null ? tower.Geom.X : 0.0
        );
    }
}
