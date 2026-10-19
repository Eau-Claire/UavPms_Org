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
using UavPms.Shared.Contracts.Events;
using UavPms.NotificationService.Application.Features.Notifications.Commands.CreateNotification;
using UavPms.NotificationService.Domain.Interfaces.Repositories;

namespace UavPms.NotificationService.Infrastructure.Messaging;

public class DefectDetectedConsumer : BackgroundService
{
    private readonly ILogger<DefectDetectedConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string ExchangeName = EventTopologyConstants.Exchange;
    private const string QueueName = EventTopologyConstants.Queues.NotificationsDefectAlert;
    private const string RoutingKey = EventTopologyConstants.RoutingKeys.AiDetectionCompleted;
    private const string LegacyRoutingKey = EventTopologyConstants.RoutingKeys.LegacyDefectDetected;

    public DefectDetectedConsumer(
        ILogger<DefectDetectedConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DefectDetectedConsumer is starting...");

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

            await _channel.QueueBindAsync(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: LegacyRoutingKey,
                cancellationToken: stoppingToken);

            // Also support legacy exchange binding
            await _channel.ExchangeDeclareAsync(
                exchange: EventTopologyConstants.LegacyExchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(
                queue: QueueName,
                exchange: EventTopologyConstants.LegacyExchange,
                routingKey: LegacyRoutingKey,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);

                    Guid recordId = Guid.Empty;
                    Guid missionId = Guid.Empty;
                    string defectType = string.Empty;
                    bool isDefect = false;

                    if (ea.RoutingKey.Equals(RoutingKey, StringComparison.OrdinalIgnoreCase))
                    {
                        var aiEvent = JsonSerializer.Deserialize<AiDetectionCompletedEvent>(json);
                        if (aiEvent != null)
                        {
                            recordId = aiEvent.AnomalyId;
                            missionId = aiEvent.MissionId;
                            defectType = aiEvent.DefectType;
                            isDefect = aiEvent.IsDefect;
                        }
                    }
                    else
                    {
                        var defectEvent = JsonSerializer.Deserialize<DefectDetectedEvent>(json);
                        if (defectEvent != null)
                        {
                            recordId = defectEvent.RecordId;
                            missionId = defectEvent.MissionId;
                            defectType = defectEvent.DefectType;
                            isDefect = defectEvent.IsDefect;
                        }
                    }

                    if (isDefect)
                    {
                        _logger.LogWarning("Received defect notification: {DefectType} on mission {MissionId}",
                            defectType, missionId);

                        using var scope = _scopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
                        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                        // Get all users with SystemAdmin or Manager roles to notify
                        var admins = await userRepository.GetUsersByRoleAsync("SystemAdmin");
                        var managers = await userRepository.GetUsersByRoleAsync("Manager");

                        var usersToNotify = admins.Concat(managers)
                            .DistinctBy(u => u.Id)
                            .ToList();

                        foreach (var user in usersToNotify)
                        {
                            await mediator.Send(new CreateNotificationCommand(
                                user.Id,
                                "CriticalAlert",
                                "DetectedAnomaly",
                                recordId,
                                "⚠️ Phát hiện khuyết tật nghiêm trọng",
                                $"Phát hiện khuyết tật '{defectType}' trong nhiệm vụ bay kiểm tra. Cần xử lý ngay."
                            ));
                        }
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing DefectDetectedEvent / AiDetectionCompletedEvent");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("DefectDetectedConsumer is now listening on queue '{QueueName}'", QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("DefectDetectedConsumer is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DefectDetectedConsumer encountered an error. It will not retry.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
