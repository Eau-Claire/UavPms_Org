using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using MediatR;
using UavPms.NotificationService.Application.Features.Notifications.Queries.GetNotifications;
using UavPms.NotificationService.Application.Features.Notifications.Queries.GetNotificationById;
using UavPms.NotificationService.Application.Features.Notifications.Queries.GetUnreadCount;
using UavPms.NotificationService.Application.Features.Notifications.Commands.DeleteNotification;
using UavPms.NotificationService.Application.Features.Notifications.Commands.MarkNotificationAsRead;
using UavPms.Shared.Contracts.Constants;

using Microsoft.AspNetCore.Authorization;

namespace UavPms.NotificationService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/notifications")]
[ApiVersion("1.0")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly ISender _mediator;

    public NotificationController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? userId = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateOwner(userId, out var ownerId);
        if (error != null)
        {
            return error;
        }

        var query = new GetNotificationsQuery(ownerId);
        var result = await _mediator.Send(query, cancellationToken);
        var items = limit > 0 ? result.Take(limit).ToList() : result;

        return Ok(new ApiResponse(true, "Notifications retrieved successfully", new
        {
            items,
            totalCount = result.Count
        }));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(
        [FromQuery] string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateOwner(userId, out var ownerId);
        if (error != null)
        {
            return error;
        }

        var count = await _mediator.Send(new GetUnreadCountQuery(ownerId), cancellationToken);
        return Ok(new ApiResponse(true, "Unread notification count retrieved successfully.", new { unreadCount = count }));
    }


    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetOwnerId(out var ownerId))
        {
            return Unauthorized();
        }

        var query = new GetNotificationByIdQuery(id, ownerId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(new ApiResponse(true, "Notification retrieved successfully", result));
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetOwnerId(out var ownerId))
        {
            return Unauthorized();
        }

        var command = new MarkNotificationAsReadCommand(id, ownerId);
        await _mediator.Send(command, cancellationToken);
        return Ok(new ApiResponse(true, "Notification marked as read successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNotification(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetOwnerId(out var ownerId))
        {
            return Unauthorized();
        }

        var command = new DeleteNotificationCommand(id, ownerId);
        await _mediator.Send(command, cancellationToken);
        return Ok(new ApiResponse(true, "Notification deleted successfully."));
    }

    private bool TryGetOwnerId(out Guid ownerId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("uid");
        return Guid.TryParse(claim, out ownerId) && ownerId != Guid.Empty;
    }

    private IActionResult? ValidateOwner(string? userId, out Guid ownerId)
    {
        if (!TryGetOwnerId(out ownerId))
        {
            return Unauthorized();
        }

        // Model binding chuyển query rỗng thành null, nhưng client đã truyền thì vẫn phải kiểm tra.
        if (userId == null && !Request.Query.ContainsKey(nameof(userId)))
        {
            return null;
        }

        if (!Guid.TryParse(userId, out var requestedId) || Request.Query[nameof(userId)].Count > 1)
        {
            return BadRequest(new ApiResponse(false, "Invalid UserId format.", ErrorCode: "VALIDATION_ERROR"));
        }

        if (requestedId != ownerId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiResponse(false, "You can only access your own notifications.", ErrorCode: "FORBIDDEN"));
        }

        return null;
    }
}
