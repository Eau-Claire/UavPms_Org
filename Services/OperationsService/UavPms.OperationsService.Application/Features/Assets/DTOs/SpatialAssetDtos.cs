namespace UavPms.OperationsService.Application.Features.Assets.DTOs;

public record SpatialAssetQueryResponse(
    int Total,
    IReadOnlyList<SpatialAssetDto> Assets);

public record SpatialAssetDto(
    Guid AssetId,
    string AssetCode,
    string Name,
    double Latitude,
    double Longitude,
    string Status)
{
    // Preserve the original response property while the GIS client migrates to assetId.
    public Guid Id => AssetId;
}
