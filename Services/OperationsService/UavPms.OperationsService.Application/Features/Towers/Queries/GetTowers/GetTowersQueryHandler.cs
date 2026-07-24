using MediatR;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Application.Features.Towers.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Towers.Queries.GetTowers;

public class GetTowersQueryHandler : IRequestHandler<GetTowersQuery, PaginatedTowersResponse>
{
    private readonly ITowerRepository _towerRepository;

    public GetTowersQueryHandler(ITowerRepository towerRepository)
    {
        _towerRepository = towerRepository;
    }
    
    public async Task<PaginatedTowersResponse> Handle(GetTowersQuery request, CancellationToken cancellationToken)
    {
        var (towers, totalCount) = await _towerRepository.GetTowersPagedAsync(
            request.Page,
            request.PageSize,
            request.LineAssetId
        );
        
        var dtos = towers.Select(t => new TowerDto(
            t.Id,
            t.LineAssetId,
            t.TowerCode,
            t.Geom != null ? t.Geom.Coordinate.Y : 0.0,
            t.Geom != null? t.Geom.Coordinate.X : 0.0
            )).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        var pagination = new PaginationMetaData(request.Page, request.PageSize, totalCount, totalPages);
        
        return new PaginatedTowersResponse(dtos, pagination);
    }
}