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
using UavPms.NotificationService.Application.Features.Notifications.Commands.CreateNotification;
using UavPms.Shared.Contracts.Events;

namespace UavPms.NotificationService.Infrastructure.Messaging;

public class MissionDispatchedConsumer : BackgroundService
{
    private readonly ILogger<MissionDispatchedConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    public MissionDispatchedConsumer(
        ILogger<MissionDispatchedConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MissionDispatchedConsumer is starting...");

        try
        {
            _connection = await _rabbitMqConnection.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await _channel.ExchangeDeclareAsync(
                exchange: EventTopologyConstants.Exchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await _channel.QueueDeclareAsync(
                queue: EventTopologyConstants.Queues.NotificationsMissionDispatch,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await _channel.QueueBindAsync(
                queue: EventTopologyConstants.Queues.NotificationsMissionDispatch,
                exchange: EventTopologyConstants.Exchange,
                routingKey: EventTopologyConstants.RoutingKeys.MissionDispatched,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    var dispatchedEvent = JsonSerializer.Deserialize<MissionDispatchedEvent>(json);

                    if (dispatchedEvent != null)
                    {
                        _logger.LogInformation("Received MissionDispatchedEvent for mission {MissionId} ({MissionCode})",
                            dispatchedEvent.MissionId, dispatchedEvent.MissionCode);

                        using var scope = _scopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

                        // Notify each assigned pilot
                        if (dispatchedEvent.AssignedPilotIds != null)
                        {
                            foreach (var pilotId in dispatchedEvent.AssignedPilotIds)
                            {
                                await mediator.Send(new CreateNotificationCommand(
                                    pilotId,
                                    "MissionDispatched",
                                    "Mission",
                                    dispatchedEvent.MissionId,
                                    "Nhiệm vụ đã được điều phối thực hiện",
                                    $"Nhiệm vụ '{dispatchedEvent.MissionCode}' ({dispatchedEvent.Title}) đã hoàn tất thẩm định điều kiện bay và sẵn sàng cất cánh."
                                ));
                            }
                        }

                        // Notify manager
                        if (dispatchedEvent.ManagerId != Guid.Empty)
                        {
                            await mediator.Send(new CreateNotificationCommand(
                                dispatchedEvent.ManagerId,
                                "MissionDispatched",
                                "Mission",
                                dispatchedEvent.MissionId,
                                "Điều phối nhiệm vụ thành công",
                                $"Nhiệm vụ '{dispatchedEvent.MissionCode}' đã được điều phối thành công sang trạng thái thực địa."
                            ));
                        }
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing MissionDispatchedEvent");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: EventTopologyConstants.Queues.NotificationsMissionDispatch,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("MissionDispatchedConsumer is now listening on queue '{QueueName}'",
                EventTopologyConstants.Queues.NotificationsMissionDispatch);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MissionDispatchedConsumer is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MissionDispatchedConsumer encountered an error. It will not retry.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
