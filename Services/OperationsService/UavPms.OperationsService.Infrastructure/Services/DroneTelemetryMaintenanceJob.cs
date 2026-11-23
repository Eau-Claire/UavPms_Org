using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UavPms.OperationsService.Application.Features.Telemetry.DTOs;

namespace UavPms.OperationsService.Infrastructure.Services;

/// <summary>
/// Periodically checks for timed-out drones (OfflineTimeoutSeconds, TR-06) and purges telemetry history older than RetentionDays (TR-05).
/// </summary>
public sealed class DroneTelemetryMaintenanceJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DroneTelemetryMaintenanceJob> _logger;
    private readonly TimeSpan _offlineCheckInterval;
    private readonly TimeSpan _retentionCheckInterval;
    private DateTime _lastRetentionPurgeUtc = DateTime.MinValue;

    public DroneTelemetryMaintenanceJob(
        IServiceScopeFactory scopeFactory,
        ILogger<DroneTelemetryMaintenanceJob> logger)
        : this(scopeFactory, logger, TimeSpan.FromSeconds(5), TimeSpan.FromHours(6))
    {
    }

    public DroneTelemetryMaintenanceJob(
        IServiceScopeFactory scopeFactory,
        ILogger<DroneTelemetryMaintenanceJob> logger,
        TimeSpan offlineCheckInterval,
        TimeSpan retentionCheckInterval)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _offlineCheckInterval = offlineCheckInterval;
        _retentionCheckInterval = retentionCheckInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DroneTelemetryMaintenanceJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var telemetryService = scope.ServiceProvider.GetRequiredService<IDroneTelemetryService>();

                await telemetryService.CheckOfflineDronesAsync(stoppingToken);

                var nowUtc = DateTime.UtcNow;
                if (nowUtc - _lastRetentionPurgeUtc >= _retentionCheckInterval)
                {
                    await telemetryService.PurgeExpiredTelemetryAsync(stoppingToken);
                    _lastRetentionPurgeUtc = nowUtc;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in DroneTelemetryMaintenanceJob.");
            }

            try
            {
                await Task.Delay(_offlineCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("DroneTelemetryMaintenanceJob stopped.");
    }
}
