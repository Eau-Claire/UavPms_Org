using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Infrastructure.Services;

/// <summary>
/// Single-node in-memory live telemetry and online-connection cache for the current single-VPS Docker Compose deployment.
/// Scaling note: when scaling OperationsService beyond 1 replica, replace this implementation with a Redis-backed
/// <see cref="IDroneLiveStateService"/> (StackExchange.Redis) and attach SignalR Redis Backplane
/// (<c>AddSignalR().AddStackExchangeRedis(...)</c>) in Program.cs so all replicas share live drone state and Hub groups.
/// </summary>
public sealed class InMemoryDroneLiveStateService : IDroneLiveStateService
{
    private readonly ConcurrentDictionary<string, DroneLiveState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (Guid UavId, Guid? MissionId, DateTime LastReceivedAtUtc)> _activeConnections = new(StringComparer.OrdinalIgnoreCase);

    public Task UpdateStatusAsync(DroneLiveStatus status, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _states.AddOrUpdate(
            status.DroneCode,
            _ => new DroneLiveState(
                status.DroneId,
                status.DroneCode,
                status.Online,
                status.Battery,
                status.LastSeenAt,
                null,
                null,
                null,
                null,
                null,
                null),
            (_, existing) => existing with
            {
                Online = status.Online,
                Battery = status.Battery ?? existing.Battery,
                LastSeenAt = status.LastSeenAt > existing.LastSeenAt ? status.LastSeenAt : existing.LastSeenAt
            });

        return Task.CompletedTask;
    }

    public Task UpdateTelemetryAsync(DroneLiveTelemetry telemetry, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _states.AddOrUpdate(
            telemetry.DroneCode,
            _ => new DroneLiveState(
                telemetry.DroneId,
                telemetry.DroneCode,
                true,
                telemetry.Battery,
                DateTime.UtcNow,
                double.IsNaN(telemetry.Latitude) ? null : telemetry.Latitude,
                double.IsNaN(telemetry.Longitude) ? null : telemetry.Longitude,
                telemetry.Altitude,
                telemetry.Speed,
                telemetry.Heading,
                telemetry.Timestamp),
            (_, existing) =>
            {
                // TR-05: Only update latest state if SourceTimestamp is newer than stored TelemetryTimestamp.
                // Do not overwrite coordinates with null when lat/lon are missing.
                if (existing.TelemetryTimestamp.HasValue && telemetry.Timestamp <= existing.TelemetryTimestamp.Value)
                {
                    return existing with { Online = true, LastSeenAt = DateTime.UtcNow };
                }

                var hasValidGps = !double.IsNaN(telemetry.Latitude) && !double.IsNaN(telemetry.Longitude);
                return existing with
                {
                    Online = true,
                    Battery = telemetry.Battery ?? existing.Battery,
                    LastSeenAt = DateTime.UtcNow,
                    Latitude = hasValidGps ? telemetry.Latitude : existing.Latitude,
                    Longitude = hasValidGps ? telemetry.Longitude : existing.Longitude,
                    Altitude = telemetry.Altitude ?? existing.Altitude,
                    Speed = telemetry.Speed ?? existing.Speed,
                    Heading = telemetry.Heading ?? existing.Heading,
                    TelemetryTimestamp = telemetry.Timestamp
                };
            });

        return Task.CompletedTask;
    }

    public Task<DroneLiveState?> GetAsync(string droneCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(droneCode))
            return Task.FromResult<DroneLiveState?>(null);

        _states.TryGetValue(droneCode.Trim(), out var state);
        return Task.FromResult(state);
    }

    /// <summary>
    /// Marks a drone as having just sent telemetry. Returns true if the drone transitioned from Offline/unknown to Online.
    /// </summary>
    public bool MarkMessageReceived(string droneCode, Guid uavId, Guid? missionId, DateTime receivedAtUtc)
    {
        var key = droneCode.Trim();
        var wasOnline = _states.TryGetValue(key, out var existing) && existing.Online;
        _activeConnections[key] = (uavId, missionId, receivedAtUtc);
        if (existing != null && !existing.Online)
        {
            _states[key] = existing with { Online = true, LastSeenAt = receivedAtUtc };
        }
        return !wasOnline;
    }

    /// <summary>
    /// Sweeps active connections and marks any drone with no message for longer than <paramref name="timeout"/> as Offline.
    /// </summary>
    public IReadOnlyList<(string DroneCode, Guid UavId, Guid? MissionId, DateTime LastReceivedAtUtc)> SweepTimedOutDrones(
        TimeSpan timeout,
        DateTime nowUtc)
    {
        var transitionedOffline = new List<(string DroneCode, Guid UavId, Guid? MissionId, DateTime LastReceivedAtUtc)>();

        foreach (var kvp in _activeConnections)
        {
            var droneCode = kvp.Key;
            var info = kvp.Value;
            if (nowUtc - info.LastReceivedAtUtc > timeout)
            {
                if (_activeConnections.TryRemove(droneCode, out _))
                {
                    if (_states.TryGetValue(droneCode, out var state) && state.Online)
                    {
                        _states[droneCode] = state with { Online = false };
                    }
                    transitionedOffline.Add((droneCode, info.UavId, info.MissionId, info.LastReceivedAtUtc));
                }
            }
        }

        return transitionedOffline;
    }
}
