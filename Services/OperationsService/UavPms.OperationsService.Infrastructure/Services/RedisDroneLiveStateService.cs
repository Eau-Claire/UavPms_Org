using System.Text.Json;
using StackExchange.Redis;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Infrastructure.Services;

public class RedisDroneLiveStateService : IDroneLiveStateService
{
    private readonly IConnectionMultiplexer? _redis;

    public RedisDroneLiveStateService(IConnectionMultiplexer? redis = null)
    {
        _redis = redis;
    }

    public async Task UpdateStatusAsync(DroneLiveStatus status, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(status.DroneCode, cancellationToken);
        var updated = new DroneLiveState(
            status.DroneId,
            status.DroneCode,
            status.Online,
            status.Battery ?? current?.Battery,
            status.LastSeenAt,
            current?.Latitude,
            current?.Longitude,
            current?.Altitude,
            current?.Speed,
            current?.Heading,
            current?.TelemetryTimestamp);

        await SetAsync(updated, ttl);
    }

    public async Task UpdateTelemetryAsync(DroneLiveTelemetry telemetry, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(telemetry.DroneCode, cancellationToken);
        var updated = new DroneLiveState(
            telemetry.DroneId,
            telemetry.DroneCode,
            true,
            telemetry.Battery ?? current?.Battery,
            telemetry.Timestamp,
            telemetry.Latitude,
            telemetry.Longitude,
            telemetry.Altitude,
            telemetry.Speed,
            telemetry.Heading,
            telemetry.Timestamp);

        await SetAsync(updated, ttl);
    }

    public async Task<DroneLiveState?> GetAsync(string droneCode, CancellationToken cancellationToken = default)
    {
        if (_redis == null)
            return null;

        var json = await _redis.GetDatabase().StringGetAsync(BuildKey(droneCode));
        return json.HasValue
            ? JsonSerializer.Deserialize<DroneLiveState>(json!)
            : null;
    }

    private async Task SetAsync(DroneLiveState state, TimeSpan ttl)
    {
        if (_redis == null)
            return;

        var json = JsonSerializer.Serialize(state);
        await _redis.GetDatabase().StringSetAsync(BuildKey(state.DroneCode), json, ttl);
    }

    private static string BuildKey(string droneCode)
    {
        return $"uav:status:{droneCode}";
    }
}
