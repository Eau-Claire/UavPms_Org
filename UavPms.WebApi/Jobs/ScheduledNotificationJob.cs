using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UavPms.Application.Features.Notifications.Commands.CreateNotification;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.WebApi.Jobs;

public class ScheduledNotificationJob
{
    private readonly ILogger<ScheduledNotificationJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ScheduledNotificationJob(ILogger<ScheduledNotificationJob> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task SendNotificationAsync(string? userIds, string title, string body, string type)
    {
        _logger.LogInformation("ScheduledNotificationJob executing for target: {Target}", userIds ?? "ALL");

        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        if (!string.IsNullOrWhiteSpace(userIds))
        {
            var idParts = userIds.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int count = 0;
            foreach (var part in idParts)
            {
                if (Guid.TryParse(part, out var userId))
                {
                    await mediator.Send(new CreateNotificationCommand(
                        userId,
                        type,
                        "Scheduled",
                        null,
                        title,
                        body
                    ));
                    _logger.LogInformation("Scheduled notification successfully sent to user {UserId}", userId);
                    count++;
                }
                else
                {
                    _logger.LogWarning("Invalid user ID format: {Part}", part);
                }
            }
            _logger.LogInformation("Finished scheduled push to {Count} specific users.", count);
        }
        else
        {
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            // Send to all active (non-deleted) users
            var users = await userRepository.GetAllAsync();
            int count = 0;
            foreach (var user in users)
            {
                // Only send to non-deleted users
                if (!user.IsDeleted)
                {
                    await mediator.Send(new CreateNotificationCommand(
                        user.Id,
                        type,
                        "ScheduledAll",
                        null,
                        title,
                        body
                    ));
                    count++;
                }
            }
            _logger.LogInformation("Scheduled notification successfully sent to all {Count} active users", count);
        }
    }
}
