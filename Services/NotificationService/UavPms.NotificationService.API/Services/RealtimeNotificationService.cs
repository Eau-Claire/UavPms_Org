using Microsoft.AspNetCore.SignalR;
using UavPms.NotificationService.Domain.Contracts;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.NotificationService.API.Hubs;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;
using AIAnalysisStatusChangedEvent = UavPms.NotificationService.Domain.Contracts.AIAnalysisStatusChangedEvent;

namespace UavPms.NotificationService.API.Services;

public class RealtimeNotificationService : IRealtimeNotificationService
{
    private const string MissionLifecycleEventName = "MissionLifecycleEvent";
    private const string AssessmentRealtimeEventName = "AssessmentRealtimeEvent";
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

            if (statusChanged.MissionId.HasValue && statusChanged.MissionId.Value != Guid.Empty)
            {
                var missionGroup = NotificationHub.MissionGroupName(statusChanged.MissionId.Value.ToString());
                await _hubContext.Clients
                    .Group(missionGroup)
                    .SendAsync(RealtimeNotificationEvents.AiAnalysisStatusChanged, statusChanged, cancellationToken);
            }

            _logger.LogInformation(
                "AI analysis status pushed. UserId={UserId}, MissionId={MissionId}, RequestId={RequestId}, Status={Status}, ConnectionIds={ConnectionIds}",
                userId, statusChanged.MissionId, statusChanged.RequestId, statusChanged.Status, connectionIds);
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
        if (evt == null || !Guid.TryParse(evt.MissionId, out var missionId))
        {
            return;
        }

        try
        {
            var targetGroups = new HashSet<string>(StringComparer.Ordinal)
            {
                NotificationHub.MissionGroupName(missionId.ToString())
            };
            AddUserGroup(targetGroups, evt.TargetUserId);
            AddUserGroup(targetGroups, evt.InspectorId);
            AddUserGroup(targetGroups, evt.ManagerId);
            foreach (var assignedUserId in evt.AssignedUserIds ?? [])
            {
                AddUserGroup(targetGroups, assignedUserId);
            }

            await SendOnceToGroupsAsync(targetGroups, MissionLifecycleEventName, evt, cancellationToken);

            _logger.LogInformation("Realtime mission event broadcasted. MissionId={MissionId}, Type={Type}", evt.MissionId, evt.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast realtime mission event. MissionId={MissionId}, Type={Type}", evt.MissionId, evt.Type);
        }
    }

    public async Task SendAssessmentEventAsync(AssessmentRealtimeEventDto evt, CancellationToken cancellationToken = default)
    {
        if (evt == null || !Guid.TryParse(evt.AssessmentId, out var assessmentId))
        {
            return;
        }

        try
        {
            var targetGroups = new[]
            {
                NotificationHub.AssessmentGroupName(assessmentId.ToString()),
                NotificationHub.AssessmentsListGroupName,
                NotificationHub.RoleGroupName(UserRoles.Manager)
            };
            await SendOnceToGroupsAsync(targetGroups, AssessmentRealtimeEventName, evt, cancellationToken);

            _logger.LogInformation("Realtime assessment event broadcasted. AssessmentId={AssessmentId}, Type={Type}", evt.AssessmentId, evt.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast realtime assessment event. AssessmentId={AssessmentId}, Type={Type}", evt.AssessmentId, evt.Type);
        }
    }

    private async Task SendOnceToGroupsAsync(
        IEnumerable<string> groupNames,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        var connectionIds = groupNames
            .SelectMany(_connectionRegistry.GetConnections)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (connectionIds.Length == 0)
        {
            return;
        }

        await _hubContext.Clients.Clients(connectionIds).SendAsync(eventName, payload, cancellationToken);
    }

    private static void AddUserGroup(ISet<string> targetGroups, string? userIdText)
    {
        if (Guid.TryParse(userIdText, out var userId))
        {
            targetGroups.Add(NotificationHub.UserGroupName(userId));
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
            IsRead = notification.ReadAt != null
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
