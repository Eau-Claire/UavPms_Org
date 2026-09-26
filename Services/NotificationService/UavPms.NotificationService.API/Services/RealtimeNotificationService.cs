using Microsoft.AspNetCore.SignalR;
using UavPms.NotificationService.Domain.Contracts;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.NotificationService.API.Hubs;

namespace UavPms.NotificationService.API.Services;

public class RealtimeNotificationService : IRealtimeNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly INotificationConnectionRegistry _connectionRegistry;
    private readonly ILogger<RealtimeNotificationService> _logger;

    public RealtimeNotificationService(
        IHubContext<NotificationHub> hubContext,
        INotificationConnectionRegistry connectionRegistry,
        ILogger<RealtimeNotificationService> logger)
    {
        _hubContext = hubContext;
        _connectionRegistry = connectionRegistry;
        _logger = logger;
    }

    public async Task SendToUserAsync(Guid userId, Notification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = NotificationHub.UserGroupName(userId);
            var connectionIds = _connectionRegistry.GetConnections(groupName);

            await _hubContext.Clients
                .Group(groupName)
                .SendAsync(RealtimeNotificationEvents.NotificationReceived, ToPayload(notification), cancellationToken);

            _logger.LogInformation(
                "Notification pushed to user. UserId={UserId}, NotificationId={NotificationId}, ConnectionIds={ConnectionIds}",
                userId, notification.Id, connectionIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Notification push failed for user. UserId={UserId}, NotificationId={NotificationId}, ConnectionIds={ConnectionIds}",
                userId, notification.Id, _connectionRegistry.GetConnections(NotificationHub.UserGroupName(userId)));
        }
    }

    public async Task SendToUsersAsync(IEnumerable<Guid> userIds, Notification notification, CancellationToken cancellationToken = default)
    {
        foreach (var userId in userIds.Distinct())
        {
            await SendToUserAsync(userId, notification, cancellationToken);
        }
    }

    public async Task SendToRoleAsync(string roleName, Notification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = NotificationHub.RoleGroupName(roleName);
            var connectionIds = _connectionRegistry.GetConnections(groupName);

            await _hubContext.Clients
                .Group(groupName)
                .SendAsync(RealtimeNotificationEvents.NotificationReceived, ToPayload(notification), cancellationToken);

            _logger.LogInformation(
                "Notification pushed to role. RoleName={RoleName}, NotificationId={NotificationId}, ConnectionIds={ConnectionIds}",
                roleName, notification.Id, connectionIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Notification push failed for role. RoleName={RoleName}, NotificationId={NotificationId}, ConnectionIds={ConnectionIds}",
                roleName, notification.Id, _connectionRegistry.GetConnections(NotificationHub.RoleGroupName(roleName)));
        }
    }

    public async Task SendAiAnalysisStatusToUserAsync(
        Guid userId,
        AIAnalysisStatusChangedEvent statusChanged,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = NotificationHub.UserGroupName(userId);
            var connectionIds = _connectionRegistry.GetConnections(groupName);

            await _hubContext.Clients
                .Group(groupName)
                .SendAsync(RealtimeNotificationEvents.AiAnalysisStatusChanged, statusChanged, cancellationToken);

            _logger.LogInformation(
                "AI analysis status pushed to user. UserId={UserId}, RequestId={RequestId}, Status={Status}, ConnectionIds={ConnectionIds}",
                userId, statusChanged.RequestId, statusChanged.Status, connectionIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "AI analysis status push failed for user. UserId={UserId}, RequestId={RequestId}, Status={Status}, ConnectionIds={ConnectionIds}",
                userId,
                statusChanged.RequestId,
                statusChanged.Status,
                _connectionRegistry.GetConnections(NotificationHub.UserGroupName(userId)));
        }
    }

    public async Task SendMissionEventAsync(
        UavPms.Shared.Contracts.Events.MissionLifecycleEventDto evt,
        CancellationToken cancellationToken = default)
    {
        if (evt == null || string.IsNullOrWhiteSpace(evt.MissionId)) return;

        try
        {
            var missionGroup = NotificationHub.MissionGroupName(evt.MissionId);

            // 1. Aggregate event to mission room
            await _hubContext.Clients.Group(missionGroup).SendAsync("MissionLifecycleEvent", evt, cancellationToken);

            // 2. Specific event broadcast
            switch (evt.Type?.ToUpperInvariant())
            {
                case "CONFIRMED":
                    await _hubContext.Clients.Group(missionGroup).SendAsync("MissionConfirmed", evt, cancellationToken);
                    break;
                case "SUSPENDED":
                    await _hubContext.Clients.Group(missionGroup).SendAsync("MissionSuspended", evt, cancellationToken);
                    break;
                case "POSTPONED":
                    await _hubContext.Clients.Group(missionGroup).SendAsync("MissionPostponed", evt, cancellationToken);
                    break;
                case "RESUMED":
                    await _hubContext.Clients.Group(missionGroup).SendAsync("MissionResumed", evt, cancellationToken);
                    break;
                case "CANCELLED":
                    await _hubContext.Clients.Group(missionGroup).SendAsync("MissionCancelled", evt, cancellationToken);
                    break;
                case "REMINDER":
                    await _hubContext.Clients.Group(missionGroup).SendAsync("MissionReminderSent", evt, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(evt.TargetUserId) && Guid.TryParse(evt.TargetUserId, out var reminderTargetGuid))
                    {
                        await _hubContext.Clients.Group(NotificationHub.UserGroupName(reminderTargetGuid))
                            .SendAsync("MissionReminderSent", evt, cancellationToken);
                    }
                    break;
                case "COMMUNICATION":
                    await _hubContext.Clients.Group(missionGroup).SendAsync("MissionCommunicationReceived", evt, cancellationToken);
                    break;
                case "DISPATCHED":
                    await _hubContext.Clients.Group(missionGroup).SendAsync("MissionDispatched", evt, cancellationToken);
                    var dispatchedUserGuids = new HashSet<Guid>();
                    if (!string.IsNullOrWhiteSpace(evt.InspectorId) && Guid.TryParse(evt.InspectorId, out var inspectorGuid))
                    {
                        dispatchedUserGuids.Add(inspectorGuid);
                    }
                    if (evt.AssignedUserIds != null)
                    {
                        foreach (var uid in evt.AssignedUserIds)
                        {
                            if (Guid.TryParse(uid, out var g)) dispatchedUserGuids.Add(g);
                        }
                    }
                    foreach (var targetGuid in dispatchedUserGuids)
                    {
                        var targetGroup = NotificationHub.UserGroupName(targetGuid);
                        await _hubContext.Clients.Group(targetGroup).SendAsync("MissionDispatched", evt, cancellationToken);
                    }
                    break;
                case "OVERDUE":
                    await _hubContext.Clients.Group(missionGroup).SendAsync("MissionConfirmationOverdue", evt, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(evt.ManagerId) && Guid.TryParse(evt.ManagerId, out var managerGuid))
                    {
                        await _hubContext.Clients.Group(NotificationHub.UserGroupName(managerGuid))
                            .SendAsync("MissionConfirmationOverdue", evt, cancellationToken);
                    }
                    break;
            }

            _logger.LogInformation("Realtime mission event broadcasted. MissionId={MissionId}, Type={Type}", evt.MissionId, evt.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast realtime mission event. MissionId={MissionId}, Type={Type}", evt.MissionId, evt.Type);
        }
    }

    private static RealtimeNotificationPayload ToPayload(Notification notification)
    {
        return new RealtimeNotificationPayload
        {
            Id = notification.Id,
            Type = notification.Type,
            Title = notification.Title,
            Body = notification.Body,
            ReferenceType = notification.ReferenceType,
            ReferenceId = notification.ReferenceId,
            Priority = ResolvePriority(notification),
            CreatedAt = notification.SentAt,
            IsRead = notification.IsRead
        };
    }

    private static string ResolvePriority(Notification notification)
    {
        if (notification.Type.Contains("Critical", StringComparison.OrdinalIgnoreCase) ||
            notification.Type.Contains("Emergency", StringComparison.OrdinalIgnoreCase) ||
            notification.ReferenceType.Contains("Emergency", StringComparison.OrdinalIgnoreCase))
        {
            return "High";
        }

        return "Normal";
    }

    private sealed class RealtimeNotificationPayload
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string ReferenceType { get; set; } = string.Empty;
        public Guid? ReferenceId { get; set; }
        public string Priority { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }
}
