using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UavPms.Shared.Contracts.Events;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Domain.Interfaces.Repositories;
using UavPms.NotificationService.Domain.Interfaces.Services;

namespace UavPms.NotificationService.Infrastructure.Messaging;

public class NotificationPushConsumer : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private readonly ILogger<NotificationPushConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    private const string QueueName = "notification.push-notification";
    private const string RoutingKey = "identity.event.notificationpushevent";

    public NotificationPushConsumer(
        ILogger<NotificationPushConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationPushConsumer is starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await _rabbitMqConnection.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await RabbitMqExchangeBinding.DeclareAndBindDualExchangeAsync(
                    channel,
                    QueueName,
                    stoppingToken,
                    RoutingKey);

                var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };
                channel.ChannelShutdownAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var json = Encoding.UTF8.GetString(body);
                        var pushEvent = JsonSerializer.Deserialize<NotificationPushEvent>(json);

                        if (pushEvent != null && pushEvent.UserId != Guid.Empty)
                        {
                            _logger.LogInformation("Received NotificationPushEvent for user {UserId}: {Title}",
                                pushEvent.UserId, pushEvent.Title);

                            using var scope = _scopeFactory.CreateScope();
                            var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
                            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                            var realtimeService = scope.ServiceProvider.GetRequiredService<IRealtimeNotificationService>();

                            var existing = await notificationRepo.GetByIdAsync(pushEvent.NotificationId);
                            Notification notification;

                            if (existing == null)
                            {
                                notification = new Notification
                                {
                                    Id = pushEvent.NotificationId != Guid.Empty ? pushEvent.NotificationId : Guid.NewGuid(),
                                    UserId = pushEvent.UserId,
                                    Type = pushEvent.Type ?? string.Empty,
                                    ReferenceType = pushEvent.ReferenceType ?? string.Empty,
                                    ReferenceId = pushEvent.ReferenceId,
                                    Title = pushEvent.Title ?? string.Empty,
                                    Body = pushEvent.Body ?? string.Empty,
                                    ReadAt = pushEvent.IsRead ? DateTime.UtcNow : null,
                                    SentAt = pushEvent.SentAt != default ? pushEvent.SentAt : DateTime.UtcNow,
                                    PushedAt = DateTime.UtcNow
                                };

                                await notificationRepo.AddAsync(notification);
                                await unitOfWork.SaveChangesAsync(stoppingToken);
                            }
                            else
                            {
                                notification = existing;
                            }

                            await realtimeService.SendToUserAsync(pushEvent.UserId, notification, stoppingToken);
                        }

                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing NotificationPushEvent");
                        await channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("NotificationPushConsumer is now listening on queue '{QueueName}'", QueueName);

                await disconnected.Task.WaitAsync(stoppingToken);
                _logger.LogWarning("NotificationPushConsumer RabbitMQ connection lost; reconnecting.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("NotificationPushConsumer is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NotificationPushConsumer disconnected; retrying in {DelaySeconds}s.", ReconnectDelay.TotalSeconds);
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(ReconnectDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
