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

public class MaintenanceTicketAssignedConsumer : BackgroundService
{
    private readonly ILogger<MaintenanceTicketAssignedConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    public MaintenanceTicketAssignedConsumer(
        ILogger<MaintenanceTicketAssignedConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MaintenanceTicketAssignedConsumer is starting...");

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
                queue: EventTopologyConstants.Queues.NotificationsTicketAssign,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await _channel.QueueBindAsync(
                queue: EventTopologyConstants.Queues.NotificationsTicketAssign,
                exchange: EventTopologyConstants.Exchange,
                routingKey: EventTopologyConstants.RoutingKeys.MaintenanceTicketAssigned,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    var ticketEvent = JsonSerializer.Deserialize<MaintenanceTicketAssignedEvent>(json);

                    if (ticketEvent != null && ticketEvent.AssignedToUserId != Guid.Empty)
                    {
                        _logger.LogInformation("Received MaintenanceTicketAssignedEvent for ticket {TicketCode} to user {UserId}",
                            ticketEvent.TicketCode, ticketEvent.AssignedToUserId);

                        using var scope = _scopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

                        await mediator.Send(new CreateNotificationCommand(
                            ticketEvent.AssignedToUserId,
                            "TicketAssigned",
                            "MaintenanceTicket",
                            ticketEvent.TicketId,
                            $"Phiếu bảo dưỡng mới: {ticketEvent.TicketCode}",
                            $"Bạn đã được phân công xử lý phiếu bảo dưỡng '{ticketEvent.TicketCode}'. Mức độ ưu tiên: {ticketEvent.Priority}. {ticketEvent.AnomalyDescription}"
                        ));
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing MaintenanceTicketAssignedEvent");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: EventTopologyConstants.Queues.NotificationsTicketAssign,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("MaintenanceTicketAssignedConsumer is now listening on queue '{QueueName}'",
                EventTopologyConstants.Queues.NotificationsTicketAssign);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MaintenanceTicketAssignedConsumer is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MaintenanceTicketAssignedConsumer encountered an error. It will not retry.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
