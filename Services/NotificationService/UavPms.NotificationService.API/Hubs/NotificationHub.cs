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
using UavPms.Shared.Contracts.Events;

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
            var hasAccess = await db.Missions.AnyAsync(m => m.Id == missionGuid && !m.IsDeleted && m.ManagerId == userId.Value)
                || await db.MissionAssignments.AnyAsync(a => a.MissionId == missionGuid && a.UserId == userId.Value && a.Status == "Active" && !a.IsDeleted);

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

    public async Task SendMissionEvent(MissionLifecycleEventDto evt)
    {
        if (evt == null || string.IsNullOrWhiteSpace(evt.MissionId)) return;
        var missionGroup = MissionGroupName(evt.MissionId);
        await Clients.Group(missionGroup).SendAsync("MissionLifecycleEvent", evt);
        if (!string.IsNullOrWhiteSpace(evt.Type))
        {
            await Clients.Group(missionGroup).SendAsync(evt.Type, evt);
        }
    }

    public async Task BroadcastMissionLifecycle(MissionLifecycleEventDto evt)
    {
        await SendMissionEvent(evt);
    }

    public static string AssessmentGroupName(string assessmentId) => $"assessment_{assessmentId}";
    public const string AssessmentsListGroupName = "assessments";

    public async Task JoinAssessmentGroup(string assessmentId)
    {
        if (string.IsNullOrWhiteSpace(assessmentId)) return;
        var groupName = AssessmentGroupName(assessmentId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _connectionRegistry.AddToGroup(groupName, Context.ConnectionId);
        _logger.LogInformation(
            "Connection joined assessment group. AssessmentId={AssessmentId}, ConnectionId={ConnectionId}",
            assessmentId, Context.ConnectionId);
    }

    public async Task LeaveAssessmentGroup(string assessmentId)
    {
        if (!string.IsNullOrWhiteSpace(assessmentId))
        {
            var groupName = AssessmentGroupName(assessmentId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _connectionRegistry.RemoveFromGroup(groupName, Context.ConnectionId);
            _logger.LogInformation(
                "Connection left assessment group. AssessmentId={AssessmentId}, ConnectionId={ConnectionId}",
                assessmentId, Context.ConnectionId);
        }
    }

    public async Task JoinAssessmentsListGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AssessmentsListGroupName);
        _connectionRegistry.AddToGroup(AssessmentsListGroupName, Context.ConnectionId);
        _logger.LogInformation(
            "Connection joined assessments list group. ConnectionId={ConnectionId}",
            Context.ConnectionId);
    }

    public async Task LeaveAssessmentsListGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AssessmentsListGroupName);
        _connectionRegistry.RemoveFromGroup(AssessmentsListGroupName, Context.ConnectionId);
        _logger.LogInformation(
            "Connection left assessments list group. ConnectionId={ConnectionId}",
            Context.ConnectionId);
    }

    public async Task SendAssessmentEvent(AssessmentRealtimeEventDto evt)
    {
        if (evt == null || string.IsNullOrWhiteSpace(evt.AssessmentId)) return;
        var assessmentGroup = AssessmentGroupName(evt.AssessmentId);
        var listGroup = AssessmentsListGroupName;
        var managerGroup = RoleGroupName(UserRoles.Manager);

        await Clients.Group(assessmentGroup).SendAsync("AssessmentRealtimeEvent", evt);
        await Clients.Group(listGroup).SendAsync("AssessmentRealtimeEvent", evt);
        await Clients.Group(managerGroup).SendAsync("AssessmentRealtimeEvent", evt);

        if (!string.IsNullOrWhiteSpace(evt.Type))
        {
            await Clients.Group(assessmentGroup).SendAsync(evt.Type, evt);
            await Clients.Group(listGroup).SendAsync(evt.Type, evt);
            await Clients.Group(managerGroup).SendAsync(evt.Type, evt);
        }
    }

    public async Task BroadcastAssessmentEvent(AssessmentRealtimeEventDto evt)
    {
        await SendAssessmentEvent(evt);
    }

    public async Task BroadcastEvent(MissionLifecycleEventDto evt)
    {
        await SendMissionEvent(evt);
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
