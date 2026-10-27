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
    private const string SystemSenderName = "Hệ Thống";
    private const string SystemSenderRole = "SYSTEM";
    private const string ActiveAssignmentStatus = "Active";

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
                    && m.OverdueNotifiedAt == null
                    && m.ConfirmationDeadline != null
                    && m.ConfirmationDeadline <= now
                    && (m.Status == "PENDING_CONFIRMATION" || m.Status == "PendingAcceptance" || m.Status == "Pending"))
                .ToListAsync();

            if (overdueMissions.Count == 0)
            {
                _logger.LogDebug("No overdue missions found.");
                return;
            }

            _logger.LogWarning("Found {Count} overdue missions requiring notification.", overdueMissions.Count);

            // Missions no longer store InspectorId: derive the inspector from the active
            // INSPECTOR/PILOT assignment (INSPECTOR preferred, earliest assignment first).
            var missionIds = overdueMissions.Select(m => m.Id).ToList();
            var inspectorAssignments = await db.MissionAssignments
                .AsNoTracking()
                .Where(a => missionIds.Contains(a.MissionId)
                    && !a.IsDeleted
                    && a.Status == ActiveAssignmentStatus
                    && (a.AssignmentRole.ToUpper() == "INSPECTOR" || a.AssignmentRole.ToUpper() == "PILOT"))
                .Select(a => new { a.MissionId, a.UserId, a.AssignmentRole, a.AssignedAt })
                .ToListAsync();
            var inspectorIds = inspectorAssignments
                .GroupBy(a => a.MissionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(a => string.Equals(a.AssignmentRole, "INSPECTOR", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                          .ThenBy(a => a.AssignedAt)
                          .First().UserId);

            var eventsToBroadcast =new System.Collections.Generic.List<(string MissionGroup, Guid ManagerId, MissionLifecycleEventDto EventDto)>();

            foreach (var mission in overdueMissions)
            {
                mission.OverdueNotifiedAt = now;
                mission.UpdatedAt = now;

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
                        SentAt = now
                    };
                    db.Notifications.Add(notification);
                }

                // 2. Add Communication Log record
                var commLog = new MissionCommunicationLog
                {
                    Id = Guid.NewGuid(),
                    MissionId = mission.Id,
                    Type = "OVERDUE",
                    Content = $"Nhiệm vụ đã quá hạn xác nhận tiếp nhận lúc {mission.ConfirmationDeadline:yyyy-MM-dd HH:mm:ss} UTC.",
                    CreatedAt = now
                };
                db.MissionCommunicationLogs.Add(commLog);

                // Prepare SignalR Real-time Event
                var eventDto = new MissionLifecycleEventDto
                {
                    MissionId = mission.Id.ToString(),
                    Type = "OVERDUE",
                    Status = mission.Status,
                    ConfirmationDeadline = mission.ConfirmationDeadline?.ToString("o"),
                    ActorRole = SystemSenderRole,
                    ActorName = SystemSenderName,
                    Timestamp = now,
                    ManagerId = mission.ManagerId.ToString(),
                    InspectorId = inspectorIds.TryGetValue(mission.Id, out var inspectorId) ? inspectorId.ToString() : null,
                    Log = new MissionCommunicationLogDto
                    {
                        Id = commLog.Id.ToString(),
                        SenderName = SystemSenderName,
                        SenderRole = SystemSenderRole,
                        Type = commLog.Type,
                        Content = commLog.Content,
                        Timestamp = commLog.CreatedAt
                    }
                };

                eventsToBroadcast.Add((NotificationHub.MissionGroupName(mission.Id.ToString()), mission.ManagerId, eventDto));
            }

            // Save database changes BEFORE broadcasting realtime events
            await db.SaveChangesAsync();
            _logger.LogInformation("Successfully processed {Count} overdue missions in database. Now broadcasting realtime notifications...", overdueMissions.Count);

            // 3. Broadcast SignalR Real-time Events after DB commit
            foreach (var (missionGroup, mgrId, eventDto) in eventsToBroadcast)
            {
                try
                {
                    await hubContext.Clients.Group(missionGroup).SendAsync("MissionConfirmationOverdue", eventDto);
                    await hubContext.Clients.Group(missionGroup).SendAsync("MissionLifecycleEvent", eventDto);

                    if (mgrId != Guid.Empty)
                    {
                        await hubContext.Clients.Group(NotificationHub.UserGroupName(mgrId))
                            .SendAsync("MissionConfirmationOverdue", eventDto);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast overdue notification via SignalR for mission {MissionId}", eventDto.MissionId);
                }
            }
            _logger.LogInformation("Successfully processed {Count} overdue missions.", overdueMissions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during MissionConfirmationOverdueJob execution.");
        }
    }
}
