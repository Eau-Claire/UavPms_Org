using System;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UavPms.NotificationService.API.Hubs;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Events;

namespace UavPms.NotificationService.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications/realtime")]
[Route("api/notifications/realtime")]
public class RealtimeMissionBroadcastController : ControllerBase
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RealtimeMissionBroadcastController> _logger;

    public RealtimeMissionBroadcastController(
        IHubContext<NotificationHub> hubContext,
        ApplicationDbContext db,
        ILogger<RealtimeMissionBroadcastController> logger)
    {
        _hubContext = hubContext;
        _db = db;
        _logger = logger;
    }

    [HttpPost("mission-event")]
    [AllowAnonymous] // Internal communication between microservices
    public async Task<IActionResult> BroadcastMissionEvent(
        [FromBody] MissionLifecycleEventDto evt,
        CancellationToken cancellationToken = default)
    {
        if (evt == null || string.IsNullOrWhiteSpace(evt.MissionId))
        {
            return BadRequest("Invalid event payload or missing mission id.");
        }

        var missionGroup = NotificationHub.MissionGroupName(evt.MissionId);

        _logger.LogInformation(
            "Broadcasting mission lifecycle event: MissionId={MissionId}, Type={Type}, Status={Status}",
            evt.MissionId, evt.Type, evt.Status);

        // 1. Broadcast aggregate event to mission room
        await _hubContext.Clients.Group(missionGroup).SendAsync("MissionLifecycleEvent", evt, cancellationToken);

        // 2. Broadcast specific events
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
                if (!string.IsNullOrWhiteSpace(evt.TargetUserId) && Guid.TryParse(evt.TargetUserId, out var reminderUserGuid))
                {
                    await _hubContext.Clients.Group(NotificationHub.UserGroupName(reminderUserGuid))
                        .SendAsync("MissionReminderSent", evt, cancellationToken);
                }
                break;
            case "COMMUNICATION":
                await _hubContext.Clients.Group(missionGroup).SendAsync("MissionCommunicationReceived", evt, cancellationToken);
                break;
            case "DISPATCHED":
                await _hubContext.Clients.Group(missionGroup).SendAsync("MissionDispatched", evt, cancellationToken);
                if (!string.IsNullOrWhiteSpace(evt.InspectorId) && Guid.TryParse(evt.InspectorId, out var inspectorGuid))
                {
                    await _hubContext.Clients.Group(NotificationHub.UserGroupName(inspectorGuid))
                        .SendAsync("MissionDispatched", evt, cancellationToken);
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

        return Ok(new { success = true, broadcastGroup = missionGroup, type = evt.Type });
    }
}
