using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UavPms.NotificationService.API.Services;
using UavPms.NotificationService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.NotificationService.API.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;
    private readonly INotificationConnectionRegistry _connectionRegistry;
    private readonly IServiceScopeFactory? _scopeFactory;

    public NotificationHub(
        ILogger<NotificationHub> logger,
        INotificationConnectionRegistry connectionRegistry,
        IServiceScopeFactory? scopeFactory = null)
    {
        _logger = logger;
        _connectionRegistry = connectionRegistry;
        _scopeFactory = scopeFactory;
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
        if (string.IsNullOrWhiteSpace(missionId) || !Guid.TryParse(missionId, out var missionGuid))
        {
            return;
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            _logger.LogWarning(
                "Unauthenticated connection attempted to join mission group. ConnectionId={ConnectionId}",
                Context.ConnectionId);
            return;
        }

        var roles = Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var isGlobal = roles.Contains(UserRoles.SystemAdmin);
        if (!isGlobal && _scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasAccess = await db.Missions.AnyAsync(m => m.Id == missionGuid && !m.IsDeleted && (
                m.ManagerId == userId.Value ||
                m.InspectorId == userId.Value ||
                m.AssignedToUserId == userId.Value
            )) || await db.MissionAssignments.AnyAsync(a => a.MissionId == missionGuid && a.UserId == userId.Value && a.Status == 1 && !a.IsDeleted);

            if (!hasAccess)
            {
                _logger.LogWarning(
                    "Forbidden attempt to join mission group. UserId={UserId}, MissionId={MissionId}, ConnectionId={ConnectionId}",
                    userId, missionId, Context.ConnectionId);
                return;
            }
        }

        var groupName = MissionGroupName(missionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _connectionRegistry.AddToGroup(groupName, Context.ConnectionId);
        _logger.LogInformation(
            "Connection joined mission group. MissionId={MissionId}, ConnectionId={ConnectionId}",
            missionId, Context.ConnectionId);
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

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub")
            ?? Context.User?.FindFirstValue("uid");
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public class NotificationsHub : NotificationHub
{
    public NotificationsHub(
        ILogger<NotificationHub> logger,
        INotificationConnectionRegistry connectionRegistry,
        IServiceScopeFactory? scopeFactory = null)
        : base(logger, connectionRegistry, scopeFactory)
    {
    }
}
