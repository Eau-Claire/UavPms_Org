namespace UavPms.AIInspectionService.Application.Features.Anomalies;

public record AnomalyTowerAssignmentDto(Guid AnomalyId, Guid TowerId, string TowerCode, string ValidationStatus);

public record TowerSuggestionDto(
    Guid TowerId,
    string TowerCode,
    Guid RegionId,
    string? Address,
    double Latitude,
    double Longitude,
    double DistanceMeters,
    bool IsCurrent);

/// <param name="Source">"Anomaly" (anomaly GPS) or "Media" (capture location of its media).</param>
public record TowerSuggestionsDto(
    Guid AnomalyId,
    Guid? CurrentTowerId,
    string Source,
    double Latitude,
    double Longitude,
    double RadiusMeters,
    IReadOnlyList<TowerSuggestionDto> Towers);
