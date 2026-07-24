namespace UavPms.OperationsService.Application.Features.Towers.DTOs;

public record TowerDto
(
    Guid Id,
    Guid LineAssetId,
    string TowerCode,
    double Latitude,
    double Longitude
);