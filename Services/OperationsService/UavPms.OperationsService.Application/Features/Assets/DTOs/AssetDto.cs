namespace UavPms.OperationsService.Application.Features.Assets.DTOs;

public record AssetDto(
    Guid Id,
    Guid TowerId,
    string AssetType,
    string AssetCode,
    string Status,
    double CurrentHealthScore,
    string RiskLevel,
    DateTime? LastInspectedAt,
    int DefectCount,
    string? TowerCode,
    Guid? LineId,
    string? LineName,
    Guid? RegionId,
    string? RegionName
);
