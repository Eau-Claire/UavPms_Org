using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UavPms.NotificationService.API.Services;

namespace UavPms.NotificationService.API.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;
    private readonly INotificationConnectionRegistry _connectionRegistry;

    public NotificationHub(
        ILogger<NotificationHub> logger,
        INotificationConnectionRegistry connectionRegistry)
    {
        _logger = logger;
        _connectionRegistry = connectionRegistry;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            _logger.LogWarning(
                "SignalR notification connection rejected because user id claim is missing. ConnectionId={ConnectionId}",
                Context.ConnectionId);
            Context.Abort();
            return;
        }

        var userGroupName = UserGroupName(userId.Value);
        await Groups.AddToGroupAsync(Context.ConnectionId, userGroupName);
        _connectionRegistry.AddToGroup(userGroupName, Context.ConnectionId);

        foreach (var role in Context.User?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct() ?? Enumerable.Empty<string>())
        {
            var roleGroupName = RoleGroupName(role);
            await Groups.AddToGroupAsync(Context.ConnectionId, roleGroupName);
            _connectionRegistry.AddToGroup(roleGroupName, Context.ConnectionId);
        }

        _logger.LogInformation(
            "User connected to notifications hub. UserId={UserId}, ConnectionId={ConnectionId}",
            userId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        if (userId != null)
        {
            _connectionRegistry.RemoveFromGroup(UserGroupName(userId.Value), Context.ConnectionId);
        }

        foreach (var role in Context.User?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct() ?? Enumerable.Empty<string>())
        {
            _connectionRegistry.RemoveFromGroup(RoleGroupName(role), Context.ConnectionId);
        }

        if (exception == null)
        {
            _logger.LogInformation(
                "User disconnected from notifications hub. UserId={UserId}, ConnectionId={ConnectionId}",
                userId, Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "User disconnected from notifications hub with error. UserId={UserId}, ConnectionId={ConnectionId}",
                userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public static string UserGroupName(Guid userId) => $"user:{userId}";

    public static string RoleGroupName(string roleName) => $"role:{roleName}";

    public static string MissionGroupName(string missionId) => $"mission_{missionId}";

    public async Task JoinMissionGroup(string missionId)
    {
        if (!string.IsNullOrWhiteSpace(missionId))
        {
            var groupName = MissionGroupName(missionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _connectionRegistry.AddToGroup(groupName, Context.ConnectionId);
            _logger.LogInformation(
                "Connection joined mission group. MissionId={MissionId}, ConnectionId={ConnectionId}",
                missionId, Context.ConnectionId);
        }
    }

    public async Task LeaveMissionGroup(string missionId)
    {
        if (!string.IsNullOrWhiteSpace(missionId))
        {
            var groupName = MissionGroupName(missionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _connectionRegistry.RemoveFromGroup(groupName, Context.ConnectionId);
            _logger.LogInformation(
                "Connection left mission group. MissionId={MissionId}, ConnectionId={ConnectionId}",
                missionId, Context.ConnectionId);
        }
    }

    public async Task SendMissionEvent(UavPms.Shared.Contracts.Events.MissionLifecycleEventDto evt)
    {
        if (evt == null || string.IsNullOrWhiteSpace(evt.MissionId)) return;

        var missionGroup = MissionGroupName(evt.MissionId);

        // 1. Broadcast aggregate event to mission room
        await Clients.Group(missionGroup).SendAsync("MissionLifecycleEvent", evt);

        // 2. Broadcast specific lifecycle events
        switch (evt.Type?.ToUpperInvariant())
        {
            case "CONFIRMED":
                await Clients.Group(missionGroup).SendAsync("MissionConfirmed", evt);
                break;
            case "SUSPENDED":
                await Clients.Group(missionGroup).SendAsync("MissionSuspended", evt);
                break;
            case "POSTPONED":
                await Clients.Group(missionGroup).SendAsync("MissionPostponed", evt);
                break;
            case "RESUMED":
                await Clients.Group(missionGroup).SendAsync("MissionResumed", evt);
                break;
            case "CANCELLED":
                await Clients.Group(missionGroup).SendAsync("MissionCancelled", evt);
                break;
            case "REMINDER":
                await Clients.Group(missionGroup).SendAsync("MissionReminderSent", evt);
                if (!string.IsNullOrWhiteSpace(evt.TargetUserId) && Guid.TryParse(evt.TargetUserId, out var reminderTargetGuid))
                {
                    await Clients.Group(UserGroupName(reminderTargetGuid)).SendAsync("MissionReminderSent", evt);
                }
                break;
            case "COMMUNICATION":
                await Clients.Group(missionGroup).SendAsync("MissionCommunicationReceived", evt);
                break;
            case "DISPATCHED":
                await Clients.Group(missionGroup).SendAsync("MissionDispatched", evt);
                if (!string.IsNullOrWhiteSpace(evt.InspectorId) && Guid.TryParse(evt.InspectorId, out var inspectorGuid))
                {
                    await Clients.Group(UserGroupName(inspectorGuid)).SendAsync("MissionDispatched", evt);
                }
                break;
            case "OVERDUE":
                await Clients.Group(missionGroup).SendAsync("MissionConfirmationOverdue", evt);
                if (!string.IsNullOrWhiteSpace(evt.ManagerId) && Guid.TryParse(evt.ManagerId, out var managerGuid))
                {
                    await Clients.Group(UserGroupName(managerGuid)).SendAsync("MissionConfirmationOverdue", evt);
                }
                break;
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public class NotificationsHub : NotificationHub
{
    public NotificationsHub(
        ILogger<NotificationHub> logger,
        INotificationConnectionRegistry connectionRegistry)
        : base(logger, connectionRegistry)
    {
    }
}
