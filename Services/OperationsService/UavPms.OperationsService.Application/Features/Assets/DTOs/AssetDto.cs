namespace UavPms.OperationsService.Application.Features.AssetComponents.DTOs;

public record AssetComponentDto(
    Guid Id,
    Guid TowerId,
    string ComponentType,
    string ComponentCode,
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
