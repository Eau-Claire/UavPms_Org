namespace UavPms.OperationsService.Application.Features.AssetComponents.DTOs;

public record AssetComponentDetailDto(
    Guid Id,
    Guid TowerId,
    string TowerCode,
    string ComponentType,
    string ComponentCode,
    string Status,
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
