using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UavPms.OperationsService.Application.Features.Assessments.Policies;
using UavPms.OperationsService.Domain.Entities;
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

        // NS-01: Ready assessments whose validity (or planned end) is reached. Same boundary as AssessmentExpiryPolicy
        // (<=), so the job and the on-read expiry agree. Other statuses are expired when read, evaluated or used.
        var now = DateTime.UtcNow;
        var expiredAssessments = await db.PreMissionAssessments
            .Where(x => !x.IsDeleted &&
                        x.Status == PreMissionAssessmentStatus.Ready &&
                        ((x.ValidUntil != null && x.ValidUntil <= now) || x.PlannedEnd <= now))
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var assessment in expiredAssessments)
        {
            var previousStatus = assessment.Status;
            if (!AssessmentExpiryPolicy.CheckAndApplyExpiry(assessment))
                continue;

            assessment.UpdatedAt = now;
            count++;
            db.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                TableName = "PreMissionAssessments",
                RecordId = assessment.Id,
                ActionType = "ASSESSMENT_EXPIRED",
                OldValues = JsonSerializer.Serialize(new { status = previousStatus.ToString(), assessment.ValidUntil }),
                NewValues = JsonSerializer.Serialize(new { status = assessment.Status.ToString(), source = "PreMissionAssessmentExpiryJob" }),
                IpAddress = string.Empty,
                UserAgent = nameof(PreMissionAssessmentExpiryJob)
            });
        }

        if (count == 0)
            return 0;

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Auto-expired {Count} pre-mission assessment(s) that exceeded ValidUntil threshold.", count);
        return count;
    }
}
