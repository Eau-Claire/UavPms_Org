using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UavPms.NotificationService.API.Hubs;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Events;

namespace UavPms.NotificationService.API.Jobs;

public class MissionConfirmationOverdueJob
{
    private readonly ILogger<MissionConfirmationOverdueJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public MissionConfirmationOverdueJob(
        ILogger<MissionConfirmationOverdueJob> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task Execute()
    {
        _logger.LogInformation("MissionConfirmationOverdueJob started scanning for overdue missions...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();

            var now = DateTime.UtcNow;

            var overdueMissions = await db.Missions
                .Where(m => !m.IsDeleted
                    && !m.IsOverdueNotified
                    && m.ConfirmationDeadline != null
                    && m.ConfirmationDeadline <= now
                    && (m.Status == "PENDING_CONFIRMATION" || m.Status == "PendingAcceptance" || m.Status == "Pending" || m.Status == "Draft"))
                .ToListAsync();

            if (overdueMissions.Count == 0)
            {
                _logger.LogDebug("No overdue missions found.");
                return;
            }

            _logger.LogWarning("Found {Count} overdue missions requiring notification.", overdueMissions.Count);

            foreach (var mission in overdueMissions)
            {
                mission.IsOverdueNotified = true;

                // 1. Create urgent Notification for Manager
                if (mission.ManagerId != Guid.Empty)
                {
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = mission.ManagerId,
                        Type = "MISSION_CONFIRMATION_OVERDUE",
                        ReferenceType = "Mission",
                        ReferenceId = mission.Id,
                        Title = $"[MF02] Nhiệm vụ {mission.MissionCode} quá hạn tiếp nhận",
                        Body = $"Phi công chưa xác nhận tiếp nhận nhiệm vụ '{mission.Title}' trước thời hạn ({mission.ConfirmationDeadline:yyyy-MM-dd HH:mm} UTC).",
                        IsRead = false,
                        SentAt = now
                    };
                    db.Notifications.Add(notification);
                }

                // 2. Add Communication Log record
                var commLog = new MissionCommunicationLog
                {
                    Id = Guid.NewGuid(),
                    MissionId = mission.Id,
                    SenderName = "Hệ Thống",
                    SenderRole = "SYSTEM",
                    Type = "OVERDUE",
                    Content = $"Nhiệm vụ đã quá hạn xác nhận tiếp nhận lúc {mission.ConfirmationDeadline:yyyy-MM-dd HH:mm:ss} UTC.",
                    CreatedAt = now
                };
                db.MissionCommunicationLogs.Add(commLog);

                // 3. Broadcast SignalR Real-time Event
                var eventDto = new MissionLifecycleEventDto
                {
                    MissionId = mission.Id.ToString(),
                    Type = "OVERDUE",
                    Status = mission.Status,
                    ConfirmationDeadline = mission.ConfirmationDeadline?.ToString("o"),
                    ActorRole = "SYSTEM",
                    ActorName = "Hệ Thống",
                    Timestamp = now,
                    ManagerId = mission.ManagerId.ToString(),
                    InspectorId = mission.InspectorId.ToString(),
                    Log = new MissionCommunicationLogDto
                    {
                        Id = commLog.Id.ToString(),
                        SenderName = commLog.SenderName,
                        SenderRole = commLog.SenderRole,
                        Type = commLog.Type,
                        Content = commLog.Content,
                        Timestamp = commLog.CreatedAt
                    }
                };

                var missionGroup = NotificationHub.MissionGroupName(mission.Id.ToString());
                await hubContext.Clients.Group(missionGroup).SendAsync("MissionConfirmationOverdue", eventDto);
                await hubContext.Clients.Group(missionGroup).SendAsync("MissionLifecycleEvent", eventDto);

                if (mission.ManagerId != Guid.Empty)
                {
                    await hubContext.Clients.Group(NotificationHub.UserGroupName(mission.ManagerId))
                        .SendAsync("MissionConfirmationOverdue", eventDto);
                }
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("Successfully processed {Count} overdue missions.", overdueMissions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during MissionConfirmationOverdueJob execution.");
        }
    }
}
