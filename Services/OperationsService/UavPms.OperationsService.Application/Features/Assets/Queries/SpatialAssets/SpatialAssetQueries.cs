using MediatR;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Features.AssetComponents.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Queries.SpatialAssets;

public record SpatialAssetQuery(Polygon Polygon) : IRequest<SpatialAssetQueryResponse>;

public record NearbyAssetsQuery(double Latitude, double Longitude, double RadiusMeters)
    : IRequest<IReadOnlyList<NearbyAssetComponentDto>>;

public record MapAssetsQuery(double MinLatitude, double MinLongitude, double MaxLatitude, double MaxLongitude)
    : IRequest<SpatialAssetQueryResponse>;

public class SpatialAssetQueryHandler : IRequestHandler<SpatialAssetQuery, SpatialAssetQueryResponse>
{
    private readonly IAssetComponentRepository _repository;

    public SpatialAssetQueryHandler(IAssetComponentRepository repository) => _repository = repository;

    public async Task<SpatialAssetQueryResponse> Handle(SpatialAssetQuery request, CancellationToken cancellationToken)
    {
        var matches = await _repository.GetAssetComponentsIntersectingAsync(request.Polygon, cancellationToken);
        var assets = matches.Select(ToSpatialDto).ToList();
        return new SpatialAssetQueryResponse(assets.Count, assets);
    }

    private static SpatialAssetComponentDto ToSpatialDto(SpatialAssetMatch match) => new(
        match.Id, match.Code, match.Name, match.Latitude, match.Longitude, match.Status);
}

public class NearbyAssetsQueryHandler : IRequestHandler<NearbyAssetsQuery, IReadOnlyList<NearbyAssetComponentDto>>
{
    private readonly IAssetComponentRepository _repository;

    public NearbyAssetsQueryHandler(IAssetComponentRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<NearbyAssetComponentDto>> Handle(NearbyAssetsQuery request, CancellationToken cancellationToken)
    {
        var matches = await _repository.GetAssetComponentsWithinDistanceAsync(
            request.Latitude, request.Longitude, request.RadiusMeters, cancellationToken);

        return matches.Select(match => new NearbyAssetComponentDto(
            match.Id,
            match.Code,
            match.Name,
            match.Latitude,
            match.Longitude,
            match.DistanceMeters ?? 0)).ToList();
    }
}

public class MapAssetsQueryHandler : IRequestHandler<MapAssetsQuery, SpatialAssetQueryResponse>
{
    private readonly IAssetComponentRepository _repository;

    public MapAssetsQueryHandler(IAssetComponentRepository repository) => _repository = repository;

    public async Task<SpatialAssetQueryResponse> Handle(MapAssetsQuery request, CancellationToken cancellationToken)
    {
        var matches = await _repository.GetAssetComponentsInBoundingBoxAsync(
            request.MinLatitude,
            request.MinLongitude,
            request.MaxLatitude,
            request.MaxLongitude,
            cancellationToken);

        var assets = matches.Select(match => new SpatialAssetComponentDto(
            match.Id, match.Code, match.Name, match.Latitude, match.Longitude, match.Status)).ToList();
        return new SpatialAssetQueryResponse(assets.Count, assets);
    }
}
