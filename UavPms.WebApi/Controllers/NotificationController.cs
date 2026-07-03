using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Asp.Versioning;
using MediatR;
using UavPms.Core.Contracts;
using UavPms.Application.Features.Notifications.Queries.GetNotifications;
using UavPms.Application.Features.Notifications.Queries.GetNotificationById;
using UavPms.Application.Features.Notifications.Commands.CreateNotification;
using UavPms.Application.Features.Notifications.Commands.DeleteNotification;
using UavPms.Application.Features.Notifications.Commands.MarkNotificationAsRead;

using Microsoft.AspNetCore.Authorization;

namespace UavPms.WebApi.Controllers;

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

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new ApiResponse(false, "UserId is required."));
        }

        if (!Guid.TryParse(userId, out var userGuid))
        {
            return BadRequest(new ApiResponse(false, "Invalid UserId format."));
        }

        var query = new GetNotificationsQuery(userGuid);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Notifications retrieved successfully", result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetNotificationByIdQuery(id);
        var result = await _mediator.Send(query);
        return Ok(new ApiResponse(true, "Notification retrieved successfully", result));
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var command = new MarkNotificationAsReadCommand(id);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Notification marked as read successfully."));
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest request)
    {
        var command = new CreateNotificationCommand(
            request.UserId, 
            request.Type, 
            request.ReferenceType, 
            request.ReferenceId, 
            request.Title, 
            request.Body
        );
        
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetHistory), new { userId = result.UserId.ToString() }, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        var command = new DeleteNotificationCommand(id);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Notification deleted successfully."));
    }

    [HttpPost("enqueue-email")]
    public IActionResult EnqueueEmail([FromBody] EnqueueEmailRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Body))
        {
            return BadRequest(new ApiResponse(false, "Email, Subject, and Body are required."));
        }

        var jobId = Hangfire.BackgroundJob.Enqueue<UavPms.Core.Interfaces.Services.IEmailService>(
            emailService => emailService.SendEmailAsync(request.Email, request.Subject, request.Body));

        return Ok(new ApiResponse(true, "Email job successfully enqueued in Hangfire.", new { JobId = jobId }));
    }

    [HttpPost("schedule")]
    public IActionResult ScheduleNotification([FromBody] ScheduleNotificationRequest request)
    {
        if (string.IsNullOrEmpty(request.Title) || string.IsNullOrEmpty(request.Body))
        {
            return BadRequest(new ApiResponse(false, "Title and Body are required."));
        }

        DateTimeOffset runAt;

        if (request.ScheduleTime.HasValue)
        {
            // If the user provided a specific date/time, convert it to a DateTimeOffset
            // If the DateTime Kind is Unspecified, treat it as local time or UTC based on preference.
            // Let's treat it as Local time (since the user is testing from Vietnam +07:00 or UTC).
            var timeValue = request.ScheduleTime.Value;
            if (timeValue.Kind == DateTimeKind.Unspecified)
            {
                // Convert unspecified/local to UTC or preserve offset
                runAt = new DateTimeOffset(timeValue, TimeSpan.FromHours(7)); // Vietnam offset
            }
            else
            {
                runAt = new DateTimeOffset(timeValue);
            }

            if (runAt < DateTimeOffset.UtcNow)
            {
                return BadRequest(new ApiResponse(false, $"ScheduleTime ({runAt:yyyy-MM-dd HH:mm:ss zzz}) cannot be in the past. Current UTC time is {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}."));
            }
        }
        else if (request.DelaySeconds.HasValue && request.DelaySeconds.Value > 0)
        {
            runAt = DateTimeOffset.UtcNow.AddSeconds(request.DelaySeconds.Value);
        }
        else
        {
            return BadRequest(new ApiResponse(false, "Either a valid DelaySeconds (greater than 0) or ScheduleTime (in the future) must be provided."));
        }
        
        var jobId = Hangfire.BackgroundJob.Schedule<UavPms.WebApi.Jobs.ScheduledNotificationJob>(
            job => job.SendNotificationAsync(request.UserId.HasValue ? request.UserId.Value.ToString() : null, request.Title, request.Body, request.Type ?? "ScheduledNotification"),
            runAt);

        return Ok(new ApiResponse(true, $"Notification scheduled to run at {runAt:yyyy-MM-dd HH:mm:ss zzz}.", new
        {
            JobId = jobId,
            Target = request.UserId.HasValue ? $"User: {request.UserId}" : "ALL Users"
        }));
    }
}

public record CreateNotificationRequest(
    Guid UserId,
    string Type,
    string? ReferenceType,
    Guid? ReferenceId,
    string Title,
    string Body
);

public record EnqueueEmailRequest(string Email, string Subject, string Body);

public record ScheduleNotificationRequest(
    Guid? UserId, 
    string Title, 
    string Body, 
    string? Type, 
    int? DelaySeconds,
    DateTime? ScheduleTime
);
