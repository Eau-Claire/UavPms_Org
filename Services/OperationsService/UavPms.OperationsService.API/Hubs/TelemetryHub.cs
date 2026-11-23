using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UavPms.OperationsService.Application.Features.Telemetry.DTOs;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Hubs;

[Authorize]
public sealed class TelemetryHub : Hub
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryHub> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public static string MissionGroupName(Guid missionId) => $"mission_{missionId:D}";
    public static string MissionGroupName(string missionId) => $"mission_{missionId.Trim().ToLowerInvariant()}";
    public static string DroneGroupName(string droneCode) => $"drone_{droneCode.Trim().ToUpperInvariant()}";

    public async Task JoinMission(string missionId)
    {
        if (string.IsNullOrWhiteSpace(missionId) || !Guid.TryParse(missionId.Trim(), out var missionGuid))
        {
            throw new HubException("INVALID_MISSION_ID");
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            _logger.LogWarning("Unauthenticated connection attempted JoinMission. ConnectionId={ConnectionId}", Context.ConnectionId);
            throw new HubException("AUTHENTICATION_REQUIRED");
        }

        var roles = GetCurrentRoles();
        var isGlobal = roles.Contains(UserRoles.SystemAdmin);
        if (!isGlobal)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var hasAccess = await db.Missions.AsNoTracking()
                .AnyAsync(m => m.Id == missionGuid && !m.IsDeleted && m.ManagerId == userId.Value)
                || await db.MissionAssignments.AsNoTracking()
                    .AnyAsync(a => a.MissionId == missionGuid
                                && a.UserId == userId.Value
                                && a.Status == MissionAssignmentStatus.Active
                                && !a.IsDeleted);

            if (!hasAccess)
            {
                _logger.LogWarning(
                    "Forbidden attempt to JoinMission on TelemetryHub. UserId={UserId}, MissionId={MissionId}, ConnectionId={ConnectionId}",
                    userId, missionGuid, Context.ConnectionId);
                throw new HubException("FORBIDDEN");
            }
        }

        var groupName = MissionGroupName(missionGuid);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation(
            "Connection joined telemetry mission group. UserId={UserId}, MissionId={MissionId}, ConnectionId={ConnectionId}",
            userId, missionGuid, Context.ConnectionId);
    }

    public Task LeaveMission(string missionId)
    {
        if (string.IsNullOrWhiteSpace(missionId) || !Guid.TryParse(missionId.Trim(), out var missionGuid))
            return Task.CompletedTask;

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, MissionGroupName(missionGuid));
    }

    public async Task JoinDrone(string droneCode)
    {
        if (string.IsNullOrWhiteSpace(droneCode))
        {
            throw new HubException("INVALID_DRONE_CODE");
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            throw new HubException("AUTHENTICATION_REQUIRED");
        }

        var roles = GetCurrentRoles();
        if (!roles.Contains(UserRoles.SystemAdmin) && !roles.Contains(UserRoles.Manager))
        {
            _logger.LogWarning(
                "Forbidden attempt to JoinDrone on TelemetryHub. UserId={UserId}, DroneCode={DroneCode}, ConnectionId={ConnectionId}",
                userId, droneCode, Context.ConnectionId);
            throw new HubException("FORBIDDEN");
        }

        var groupName = DroneGroupName(droneCode);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation(
            "Connection joined telemetry drone group. UserId={UserId}, DroneCode={DroneCode}, ConnectionId={ConnectionId}",
            userId, droneCode, Context.ConnectionId);
    }

    public Task LeaveDrone(string droneCode)
    {
        if (string.IsNullOrWhiteSpace(droneCode))
            return Task.CompletedTask;

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, DroneGroupName(droneCode));
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub")
            ?? Context.User?.FindFirstValue("uid");
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private HashSet<string> GetCurrentRoles() =>
        Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase)
        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class TelemetryRealtimeNotifier : ITelemetryRealtimeNotifier
{
    private readonly IHubContext<TelemetryHub> _hubContext;

    public TelemetryRealtimeNotifier(IHubContext<TelemetryHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyTelemetryUpdatedAsync(DroneTelemetryDto telemetry, CancellationToken ct = default)
    {
        var tasks = new List<Task>(2)
        {
            _hubContext.Clients
                .Group(TelemetryHub.DroneGroupName(telemetry.DroneId))
                .SendAsync("TelemetryUpdated", telemetry, ct)
        };

        if (telemetry.MissionId.HasValue && telemetry.MissionId.Value != Guid.Empty)
        {
            tasks.Add(_hubContext.Clients
                .Group(TelemetryHub.MissionGroupName(telemetry.MissionId.Value))
                .SendAsync("TelemetryUpdated", telemetry, ct));
        }

        await Task.WhenAll(tasks);
    }

    public async Task NotifyDroneOnlineAsync(DroneConnectionEventDto evt, CancellationToken ct = default)
    {
        var tasks = new List<Task>(2)
        {
            _hubContext.Clients
                .Group(TelemetryHub.DroneGroupName(evt.DroneCode))
                .SendAsync("DroneOnline", evt, ct)
        };

        if (evt.MissionId.HasValue && evt.MissionId.Value != Guid.Empty)
        {
            tasks.Add(_hubContext.Clients
                .Group(TelemetryHub.MissionGroupName(evt.MissionId.Value))
                .SendAsync("DroneOnline", evt, ct));
        }

        await Task.WhenAll(tasks);
    }

    public async Task NotifyDroneOfflineAsync(DroneConnectionEventDto evt, CancellationToken ct = default)
    {
        var tasks = new List<Task>(2)
        {
            _hubContext.Clients
                .Group(TelemetryHub.DroneGroupName(evt.DroneCode))
                .SendAsync("DroneOffline", evt, ct)
        };

        if (evt.MissionId.HasValue && evt.MissionId.Value != Guid.Empty)
        {
            tasks.Add(_hubContext.Clients
                .Group(TelemetryHub.MissionGroupName(evt.MissionId.Value))
                .SendAsync("DroneOffline", evt, ct));
        }

        await Task.WhenAll(tasks);
    }
}
