using System;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Infrastructure.Services;

/// <summary>
/// No-op implementation of IDroneLiveStateService now that real-time MQTT telemetry streaming is out of scope.
/// </summary>
public class NoOpDroneLiveStateService : IDroneLiveStateService
{
    public Task UpdateStatusAsync(DroneLiveStatus status, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task UpdateTelemetryAsync(DroneLiveTelemetry telemetry, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<DroneLiveState?> GetAsync(string droneCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<DroneLiveState?>(null);
    }
}
