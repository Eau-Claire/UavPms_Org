using System;
using System.Linq;
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
using UavPms.NotificationService.Domain.Interfaces.Repositories;
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Events;

namespace UavPms.NotificationService.Infrastructure.Messaging;

public class MaintenanceTicketAssignedConsumer : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private readonly ILogger<MaintenanceTicketAssignedConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await _rabbitMqConnection.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.ExchangeDeclareAsync(
                    exchange: EventTopologyConstants.Exchange,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: EventTopologyConstants.Queues.NotificationsTicketAssign,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.QueueBindAsync(
                    queue: EventTopologyConstants.Queues.NotificationsTicketAssign,
                    exchange: EventTopologyConstants.Exchange,
                    routingKey: EventTopologyConstants.RoutingKeys.MaintenanceTicketAssigned,
                    cancellationToken: stoppingToken);

                var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };
                channel.ChannelShutdownAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };

                var consumer = new AsyncEventingBasicConsumer(channel);
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
                            var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
                            var existing = (await notificationRepo.FindAsync(n =>
                                n.UserId == ticketEvent.AssignedToUserId &&
                                n.ReferenceId == ticketEvent.TicketId &&
                                n.ReferenceType == "MaintenanceTicket"))?.FirstOrDefault();

                            if (existing != null)
                            {
                                var realtimeService = scope.ServiceProvider.GetRequiredService<IRealtimeNotificationService>();
                                await realtimeService.SendToUserAsync(existing.UserId, existing, stoppingToken);
                            }
                            else
                            {
                                var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
                                await mediator.Send(new CreateNotificationCommand(
                                    ticketEvent.AssignedToUserId,
                                    "TicketAssigned",
                                    "MaintenanceTicket",
                                    ticketEvent.TicketId,
                                    $"Phiếu bảo dưỡng mới: {ticketEvent.TicketCode}",
                                    $"Bạn đã được phân công xử lý phiếu bảo dưỡng '{ticketEvent.TicketCode}'. Mức độ ưu tiên: {ticketEvent.Priority}. {ticketEvent.AnomalyDescription}"
                                ), stoppingToken);
                            }
                        }

                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing MaintenanceTicketAssignedEvent");
                        await channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: EventTopologyConstants.Queues.NotificationsTicketAssign,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("MaintenanceTicketAssignedConsumer is now listening on queue '{QueueName}'",
                    EventTopologyConstants.Queues.NotificationsTicketAssign);

                await disconnected.Task.WaitAsync(stoppingToken);
                _logger.LogWarning("MaintenanceTicketAssignedConsumer RabbitMQ connection lost; reconnecting.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("MaintenanceTicketAssignedConsumer is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaintenanceTicketAssignedConsumer disconnected; retrying in {DelaySeconds}s.", ReconnectDelay.TotalSeconds);
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
