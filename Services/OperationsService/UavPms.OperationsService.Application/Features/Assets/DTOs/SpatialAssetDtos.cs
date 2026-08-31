namespace UavPms.OperationsService.Application.Features.AssetComponents.DTOs;

public record GeoJsonGeometryDto(string? Type, double[][][]? Coordinates);

public record SpatialQueryRequest(GeoJsonGeometryDto? Geometry);

public record SpatialAssetComponentDto(
    Guid Id,
    string Code,
    string Name,
    double Latitude,
    double Longitude,
    string Status);

public record SpatialAssetQueryResponse(int Total, IReadOnlyList<SpatialAssetComponentDto> AssetComponents);

public record NearbyAssetComponentDto(
    Guid Id,
    string Code,
    string Name,
    double Latitude,
    double Longitude,
    double DistanceMeters);
