using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UavPms.Core.Interfaces.Repositories;
using MediatR;
using UavPms.Application.Features.Notifications.Commands.CreateNotification;

namespace UavPms.WebApi.Jobs;

public class DailySummaryJob
{
    private readonly ILogger<DailySummaryJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DailySummaryJob(ILogger<DailySummaryJob> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task Execute()
    {
        _logger.LogInformation("DailySummaryJob started at {Time}", DateTime.UtcNow);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var monitorRepository = scope.ServiceProvider.GetRequiredService<IMonitorRepository>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

            // Get dashboard summary and recent defects
            var summary = await monitorRepository.GetSummaryAsync(CancellationToken.None);
            var (recentDefects, totalDefects) = await monitorRepository.GetRecentDefectsAsync(1, 10, CancellationToken.None);
            var activeAlerts = await monitorRepository.GetActiveAlertsAsync(CancellationToken.None);

            var summaryBody = $"Tổng kết hệ thống UAV-PMS ngày {DateTime.UtcNow.AddHours(7):dd/MM/yyyy}:\n" +
                              $"• Tổng nhiệm vụ: {summary.TotalMissions}\n" +
                              $"• Tổng kiểm tra: {summary.TotalInspections}\n" +
                              $"• Cảnh báo đang xử lý: {activeAlerts.Count}\n" +
                              $"• Khuyết tật phát hiện: {summary.TotalDefects} (nghiêm trọng: {summary.CriticalDefects})";

            // Send summary notification to all admins and managers
            var admins = await userRepository.GetUsersByRoleAsync("SystemAdmin");
            var managers = await userRepository.GetUsersByRoleAsync("Manager");

            var usersToNotify = admins.Concat(managers)
                .DistinctBy(u => u.Id)
                .ToList();

            foreach (var user in usersToNotify)
            {
                await mediator.Send(new CreateNotificationCommand(
                    user.Id,
                    "DailySummary",
                    "System",
                    null,
                    "📊 Báo cáo tổng hợp hàng ngày",
                    summaryBody
                ));
            }

            _logger.LogInformation("DailySummaryJob completed. Notified {Count} users.", usersToNotify.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during DailySummaryJob execution.");
        }
    }
}