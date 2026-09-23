using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UavPms.NotificationService.API.Jobs;

public class MissionConfirmationOverdueHostedService : BackgroundService
{
    private readonly ILogger<MissionConfirmationOverdueHostedService> _logger;
    private readonly MissionConfirmationOverdueJob _job;

    public MissionConfirmationOverdueHostedService(
        ILogger<MissionConfirmationOverdueHostedService> logger,
        MissionConfirmationOverdueJob job)
    {
        _logger = logger;
        _job = job;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MissionConfirmationOverdueHostedService started (Hangfire fallback worker).");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await _job.Execute();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MissionConfirmationOverdueHostedService is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in MissionConfirmationOverdueHostedService.");
        }
    }
}
