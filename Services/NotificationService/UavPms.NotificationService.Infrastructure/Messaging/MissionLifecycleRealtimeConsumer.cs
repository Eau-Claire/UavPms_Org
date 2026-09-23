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
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Events;

namespace UavPms.NotificationService.Infrastructure.Messaging;

public class MissionLifecycleRealtimeConsumer : BackgroundService
{
    private readonly ILogger<MissionLifecycleRealtimeConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string ExchangeName = "identity-exchange";
    private const string QueueName = "notification.mission-lifecycle-realtime";
    private const string RoutingKey = "identity.event.missionlifecyclerealtimeevent";

    public MissionLifecycleRealtimeConsumer(
        ILogger<MissionLifecycleRealtimeConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MissionLifecycleRealtimeConsumer is starting...");

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
                    var realtimeEvent = JsonSerializer.Deserialize<MissionLifecycleRealtimeEvent>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (realtimeEvent?.Event != null && !string.IsNullOrWhiteSpace(realtimeEvent.Event.MissionId))
                    {
                        var evt = realtimeEvent.Event;
                        _logger.LogInformation("Received MissionLifecycleRealtimeEvent for mission {MissionId}, type {Type}", evt.MissionId, evt.Type);

                        using var scope = _scopeFactory.CreateScope();
                        var realtimeService = scope.ServiceProvider.GetRequiredService<IRealtimeNotificationService>();
                        await realtimeService.SendMissionEventAsync(evt, stoppingToken);
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing MissionLifecycleRealtimeEvent");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("MissionLifecycleRealtimeConsumer is now listening on queue '{QueueName}'", QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MissionLifecycleRealtimeConsumer is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MissionLifecycleRealtimeConsumer encountered an error.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
