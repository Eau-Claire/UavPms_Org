using Microsoft.AspNetCore.SignalR;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.AIInspectionService.API.Hubs;

namespace UavPms.AIInspectionService.API.Services;

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
