namespace UavPms.OperationsService.Domain.Interfaces.Services;

public interface IDroneLiveStateService
{
    Task UpdateStatusAsync(DroneLiveStatus status, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task UpdateTelemetryAsync(DroneLiveTelemetry telemetry, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<DroneLiveState?> GetAsync(string droneCode, CancellationToken cancellationToken = default);
}

public sealed record DroneLiveStatus(
    Guid DroneId,
    string DroneCode,
    bool Online,
    double? Battery,
    DateTime LastSeenAt);

public sealed record DroneLiveTelemetry(
    Guid DroneId,
    string DroneCode,
    DateTime Timestamp,
    double Latitude,
    double Longitude,
    double? Altitude,
    double? Battery,
    double? Speed,
    double? Heading);

public sealed record DroneLiveState(
    Guid DroneId,
    string DroneCode,
    bool Online,
    double? Battery,
    DateTime LastSeenAt,
    double? Latitude,
    double? Longitude,
    double? Altitude,
    double? Speed,
    double? Heading,
    DateTime? TelemetryTimestamp);
