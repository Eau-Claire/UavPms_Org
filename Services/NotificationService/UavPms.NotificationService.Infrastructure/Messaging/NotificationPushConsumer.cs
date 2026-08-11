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
    private readonly ILogger<NotificationPushConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string ExchangeName = "identity-exchange";
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

        try
        {
            _connection = await _rabbitMqConnection.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await _channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await _channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await _channel.QueueBindAsync(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: RoutingKey,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
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
                                IsRead = pushEvent.IsRead,
                                SentAt = pushEvent.SentAt != default ? pushEvent.SentAt : DateTime.UtcNow,
                                IsPushed = true,
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

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing NotificationPushEvent");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("NotificationPushConsumer is now listening on queue '{QueueName}'", QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("NotificationPushConsumer is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationPushConsumer encountered an error. It will not retry.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
