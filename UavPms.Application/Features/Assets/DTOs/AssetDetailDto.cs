namespace UavPms.Application.Features.Assets.DTOs;

public record AssetDetailDto(
    Guid Id,
    Guid TowerId,
    string TowerCode,
    string AssetType,
    string AssetCode,
    string status,
    double CurrentHealthScore,
    string RiskLevel,
    DateTime? LastInspectedAt,
    List<ActiveAnomalyDto> ActiveAnomalies
);

public record  ActiveAnomalyDto(
    Guid Id,
    string CategoryName,
    double ConfidenceScore,
    string ValidationStatus,
    DateTime CreatedAt
);