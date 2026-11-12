using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UavPms.OperationsService.Application.Features.Schedules;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Services;

/// <summary>
/// Periodically evaluates active <see cref="Domain.Entities.InspectionSchedule"/> records
/// and triggers draft pre-mission assessments when an occurrence is due.
/// </summary>
public sealed class InspectionScheduleRunnerJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InspectionScheduleRunnerJob> _logger;

    public InspectionScheduleRunnerJob(
        IServiceScopeFactory scopeFactory,
        ILogger<InspectionScheduleRunnerJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InspectionScheduleRunnerJob cycle failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task<int> RunOnceAsync(DateTime utcNow, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scheduleService = scope.ServiceProvider.GetRequiredService<InspectionScheduleService>();

        var activeSchedules = await db.InspectionSchedules
            .Include(s => s.Region)
            .Where(s => s.IsActive && !s.IsDeleted)
            .ToListAsync(ct);

        if (activeSchedules.Count == 0)
            return 0;

        var scheduleIds = activeSchedules.Select(s => s.Id).ToList();
        var latestTriggers = await db.AuditLogs
            .AsNoTracking()
            .Where(a => a.TableName == "InspectionSchedules" &&
                        a.ActionType == "SCHEDULE_TRIGGERED" &&
                        scheduleIds.Contains(a.RecordId))
            .GroupBy(a => a.RecordId)
            .Select(g => new { ScheduleId = g.Key, LastTriggeredAt = g.Max(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.ScheduleId, x => (DateTime?)x.LastTriggeredAt, ct);

        var triggered = 0;
        foreach (var schedule in activeSchedules)
        {
            latestTriggers.TryGetValue(schedule.Id, out var lastTriggered);
            if (!RecurrenceRuleEvaluator.IsDueAt(schedule, utcNow, lastTriggered))
                continue;

            try
            {
                var result = await scheduleService.TriggerFromRunnerAsync(schedule, utcNow, ct);
                if (result != null)
                {
                    triggered++;
                    _logger.LogInformation(
                        "Scheduled inspection triggered: ScheduleId={ScheduleId}, AssessmentId={AssessmentId}, Towers={TowerCount}",
                        schedule.Id, result.AssessmentId, result.TowerCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to trigger inspection schedule {ScheduleId}", schedule.Id);
            }
        }

        return triggered;
    }
}

