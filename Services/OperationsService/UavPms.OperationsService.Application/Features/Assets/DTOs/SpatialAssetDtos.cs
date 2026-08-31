namespace UavPms.OperationsService.Application.Features.Assets.DTOs;

public record SpatialAssetQueryResponse(
    int Total,
    IReadOnlyList<SpatialAssetDto> Assets);

public record SpatialAssetDto(
    Guid Id,
    string AssetCode,
    string Name,
    double Latitude,
    double Longitude,
    string Status);
