using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class PreMissionAssessmentExpiryJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PreMissionAssessmentExpiryJob> _logger;
    private readonly TimeSpan _checkInterval;

    public PreMissionAssessmentExpiryJob(
        IServiceScopeFactory scopeFactory,
        ILogger<PreMissionAssessmentExpiryJob> logger)
        : this(scopeFactory, logger, TimeSpan.FromMinutes(2))
    {
    }

    public PreMissionAssessmentExpiryJob(
        IServiceScopeFactory scopeFactory,
        ILogger<PreMissionAssessmentExpiryJob> logger,
        TimeSpan checkInterval)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _checkInterval = checkInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PreMissionAssessmentExpiryJob started with interval {Interval}.", _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepExpiredAssessmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sweeping expired pre-mission assessments.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("PreMissionAssessmentExpiryJob stopped.");
    }

    public async Task<int> SweepExpiredAssessmentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var expiredAssessments = await db.PreMissionAssessments
            .Where(x => !x.IsDeleted &&
                        x.Status == PreMissionAssessmentStatus.Ready &&
                        x.ValidUntil.HasValue &&
                        x.ValidUntil.Value < now)
            .ToListAsync(cancellationToken);

        if (expiredAssessments.Count == 0)
            return 0;

        foreach (var assessment in expiredAssessments)
        {
            assessment.Status = PreMissionAssessmentStatus.Expired;
            assessment.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Auto-expired {Count} pre-mission assessment(s) that exceeded ValidUntil threshold.", expiredAssessments.Count);
        return expiredAssessments.Count;
    }
}
