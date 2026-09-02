using MediatR;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Features.Assets.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Assets.Queries.SpatialAssets;

public record SpatialAssetQuery(Polygon Polygon, Guid? ManagementUnitId, Guid? PowerLineId, string? AssetType) : IRequest<SpatialAssetQueryResponse>;

public class SpatialAssetQueryHandler : IRequestHandler<SpatialAssetQuery, SpatialAssetQueryResponse>
{
    private readonly IAssetRepository _repository;

    public SpatialAssetQueryHandler(IAssetRepository repository)
    {
        _repository = repository;
    }

    public async Task<SpatialAssetQueryResponse> Handle(
        SpatialAssetQuery request,
        CancellationToken cancellationToken)
    {
        var matches = await _repository.GetAssetsIntersectingAsync(request.Polygon, request.ManagementUnitId, request.PowerLineId, request.AssetType, cancellationToken);
        var assets = matches
            .Select(match => new SpatialAssetDto(
                match.Id,
                match.AssetCode,
                match.Name,
                match.Latitude,
                match.Longitude,
                match.Status))
            .ToList();

        return new SpatialAssetQueryResponse(assets.Count, assets);
    }
}
