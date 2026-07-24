using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UavPms.NotificationService.Domain.Interfaces.Repositories;
using UavPms.NotificationService.Domain.Interfaces.Services;

namespace UavPms.NotificationService.API.Jobs;

public class PushNotificationsJob
{
    private readonly ILogger<PushNotificationsJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public PushNotificationsJob(ILogger<PushNotificationsJob> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task Execute()
    {
        _logger.LogInformation("PushNotificationsJob started scanning for unpushed notifications...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var notificationRepository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Fetch all notifications not pushed yet
            var unpushedNotifications = await notificationRepository.GetUnpushedNotificationsWithActiveUserAsync();

            if (unpushedNotifications.Count == 0)
            {
                _logger.LogInformation("No new unpushed notifications found.");
                return;
            }

            _logger.LogInformation("Found {Count} unpushed notifications to dispatch.", unpushedNotifications.Count);

            foreach (var notification in unpushedNotifications)
            {
                try
                {
                    var user = notification.User;
                    if (user != null && !string.IsNullOrEmpty(user.Email))
                    {
                        _logger.LogInformation("Pushing notification '{Title}' to email {Email}", notification.Title, user.Email);
                        
                        // Send the email push notification
                        await emailService.SendEmailAsync(
                            user.Email,
                            notification.Title,
                            notification.Body
                        );
                    }
                    else
                    {
                        _logger.LogWarning("Notification {NotificationId} cannot be pushed. User email is null or missing.", notification.Id);
                    }

                    // Mark as pushed regardless of user email missing (to avoid infinite retries on invalid users)
                    notification.IsPushed = true;
                    notification.PushedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to push notification {NotificationId} to user email.", notification.Id);
                    // Do not mark as pushed so it can retry next time, or we can handle retry logic
                }
            }

            // Save all updates to DB
            await unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Successfully completed pushing pending notifications.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during PushNotificationsJob execution.");
        }
    }
}
