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
    private const string ActiveAssignmentStatus = "Active";
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
        _connectionRegistry.RemoveConnection(Context.ConnectionId);

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
            throw new HubException("Mission group is invalid.");
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            _logger.LogWarning(
                "Unauthenticated connection attempted to join mission group. ConnectionId={ConnectionId}",
                Context.ConnectionId);
            throw new HubException("User identity is invalid.");
        }

        if (!HasRole(UserRoles.SystemAdmin) && !await CanJoinMissionAsync(missionGuid, userId.Value))
        {
            throw new HubException("Mission access is denied.");
        }

        var groupName = MissionGroupName(missionGuid.ToString("D"));
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _connectionRegistry.AddToGroup(groupName, Context.ConnectionId);
        _logger.LogInformation(
            "Connection joined mission group. MissionId={MissionId}, ConnectionId={ConnectionId}",
            missionId, Context.ConnectionId);
    }

    private async Task<bool> CanJoinMissionAsync(Guid missionId, Guid userId)
    {
        var isManager = HasRole(UserRoles.Manager);
        var isInspector = HasRole(UserRoles.Inspector);
        if ((!isManager && !isInspector) || _scopeFactory == null)
        {
            return false;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Missions.AnyAsync(mission => mission.Id == missionId && !mission.IsDeleted
            && ((isManager && mission.ManagerId == userId)
                || (isInspector && mission.Assignments.Any(assignment => assignment.UserId == userId
                    && assignment.Status == ActiveAssignmentStatus && !assignment.IsDeleted))));
    }

    public async Task LeaveMissionGroup(string missionId)
    {
        if (Guid.TryParse(missionId, out var missionGuid))
        {
            var groupName = MissionGroupName(missionGuid.ToString("D"));
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _connectionRegistry.RemoveFromGroup(groupName, Context.ConnectionId);
            _logger.LogInformation(
                "Connection left mission group. MissionId={MissionId}, ConnectionId={ConnectionId}",
                missionId, Context.ConnectionId);
        }
    }

    public static string AssessmentGroupName(string assessmentId) => $"assessment_{assessmentId}";
    public const string AssessmentsListGroupName = "assessments";

    public async Task JoinAssessmentGroup(string assessmentId)
    {
        var assessmentGuid = RequireAssessmentAccess(assessmentId);
        var groupName = AssessmentGroupName(assessmentGuid.ToString("D"));
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _connectionRegistry.AddToGroup(groupName, Context.ConnectionId);
        _logger.LogInformation(
            "Connection joined assessment group. AssessmentId={AssessmentId}, ConnectionId={ConnectionId}",
            assessmentId, Context.ConnectionId);
    }

    public async Task LeaveAssessmentGroup(string assessmentId)
    {
        if (Guid.TryParse(assessmentId, out var assessmentGuid))
        {
            var groupName = AssessmentGroupName(assessmentGuid.ToString("D"));
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _connectionRegistry.RemoveFromGroup(groupName, Context.ConnectionId);
            _logger.LogInformation(
                "Connection left assessment group. AssessmentId={AssessmentId}, ConnectionId={ConnectionId}",
                assessmentId, Context.ConnectionId);
        }
    }

    public async Task JoinAssessmentsListGroup()
    {
        RequireAssessmentAccess();
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

    private Guid RequireAssessmentAccess(string assessmentId)
    {
        RequireAssessmentAccess();
        if (!Guid.TryParse(assessmentId, out var assessmentGuid))
        {
            throw new HubException("Assessment group is invalid.");
        }

        return assessmentGuid;
    }

    private void RequireAssessmentAccess()
    {
        if (GetCurrentUserId() == null)
        {
            throw new HubException("User identity is invalid.");
        }

        if (!HasRole(UserRoles.SystemAdmin) && !HasRole(UserRoles.Manager) && !HasRole(UserRoles.Inspector))
        {
            throw new HubException("Assessment access is denied.");
        }
    }

    private bool HasRole(string role)
    {
        return Context.User?.FindAll(ClaimTypes.Role)
            .Any(claim => string.Equals(claim.Value, role, StringComparison.OrdinalIgnoreCase)) == true;
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
