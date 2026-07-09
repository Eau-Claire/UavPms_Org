using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UavPms.Application.Features.Notifications.Commands.CreateNotification;
using UavPms.Core.Contracts;

namespace UavPms.Infrastructure.Messaging;

public class MissionCreatedConsumer : BackgroundService
{
    private readonly ILogger<MissionCreatedConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string ExchangeName = "identity-exchange";
    private const string QueueName = "notification.mission-created";
    private const string RoutingKey = "identity.event.missioncreatedevent";

    public MissionCreatedConsumer(
        ILogger<MissionCreatedConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MissionCreatedConsumer is starting...");

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
                    var missionEvent = JsonSerializer.Deserialize<MissionCreatedEvent>(json);

                    if (missionEvent != null)
                    {
                        _logger.LogInformation("Received MissionCreatedEvent for mission {MissionId}", missionEvent.MissionId);

                        using var scope = _scopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

                        // Notify the assigned inspector
                        await mediator.Send(new CreateNotificationCommand(
                            missionEvent.AssignedToUserId,
                            "MissionAssigned",
                            "Mission",
                            missionEvent.MissionId,
                            "Nhiệm vụ mới được giao",
                            $"Bạn đã được giao nhiệm vụ bay kiểm tra '{missionEvent.MissionCode}'. {missionEvent.Description}"
                        ));

                        // Notify the manager
                        if (missionEvent.ManagerId != Guid.Empty && missionEvent.ManagerId != missionEvent.AssignedToUserId)
                        {
                            await mediator.Send(new CreateNotificationCommand(
                                missionEvent.ManagerId,
                                "MissionCreated",
                                "Mission",
                                missionEvent.MissionId,
                                "Nhiệm vụ mới đã tạo",
                                $"Nhiệm vụ '{missionEvent.MissionCode}' đã được tạo và phân công thành công."
                            ));
                        }
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing MissionCreatedEvent");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("MissionCreatedConsumer is now listening on queue '{QueueName}'", QueueName);

            // Keep alive until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MissionCreatedConsumer is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MissionCreatedConsumer encountered an error. It will not retry.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
