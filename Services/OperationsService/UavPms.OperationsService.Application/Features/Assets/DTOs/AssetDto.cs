namespace UavPms.OperationsService.Application.Features.Assets.DTOs;

public record AssetDto(
    Guid Id,
    Guid TowerId,
    string AssetType,
    string AssetCode,
    string Status,
    double CurrentHealthScore,
    string RiskLevel,
    DateTime? LastInspectedAt
);