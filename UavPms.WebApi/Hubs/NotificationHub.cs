using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UavPms.WebApi.Services;

namespace UavPms.WebApi.Hubs;

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

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
