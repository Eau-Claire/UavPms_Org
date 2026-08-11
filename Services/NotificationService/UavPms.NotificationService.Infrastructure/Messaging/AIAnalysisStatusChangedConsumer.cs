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
using DomainAIEvent = UavPms.NotificationService.Domain.Contracts.AIAnalysisStatusChangedEvent;
using UavPms.NotificationService.Domain.Interfaces.Services;

namespace UavPms.NotificationService.Infrastructure.Messaging;

public class AIAnalysisStatusChangedConsumer : BackgroundService
{
    private readonly ILogger<AIAnalysisStatusChangedConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string ExchangeName = "identity-exchange";
    private const string QueueName = "notification.ai-analysis-status-changed";
    private const string RoutingKey = "identity.event.aianalysisstatuschangedevent";

    public AIAnalysisStatusChangedConsumer(
        ILogger<AIAnalysisStatusChangedConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AIAnalysisStatusChangedConsumer is starting...");

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
                    var aiEvent = JsonSerializer.Deserialize<AIAnalysisStatusChangedEvent>(json);

                    if (aiEvent != null && aiEvent.UserId != Guid.Empty)
                    {
                        _logger.LogInformation(
                            "Received AIAnalysisStatusChangedEvent for user {UserId}, RequestId={RequestId}, Status={Status}",
                            aiEvent.UserId, aiEvent.RequestId, aiEvent.Status);

                        using var scope = _scopeFactory.CreateScope();
                        var realtimeService = scope.ServiceProvider.GetRequiredService<IRealtimeNotificationService>();

                        var domainEvent = new DomainAIEvent
                        {
                            RequestId = aiEvent.RequestId,
                            BatchId = aiEvent.BatchId,
                            MissionId = aiEvent.MissionId,
                            MediaId = aiEvent.MediaId,
                            MediaType = aiEvent.MediaType ?? string.Empty,
                            Status = aiEvent.Status ?? string.Empty,
                            SavedDetections = aiEvent.SavedDetections,
                            CreatedAlerts = aiEvent.CreatedAlerts,
                            ErrorCode = aiEvent.ErrorCode,
                            ErrorMessage = aiEvent.ErrorMessage,
                            CreatedAt = aiEvent.CreatedAt,
                            CompletedAt = aiEvent.CompletedAt
                        };

                        await realtimeService.SendAiAnalysisStatusToUserAsync(aiEvent.UserId, domainEvent, stoppingToken);
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing AIAnalysisStatusChangedEvent");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("AIAnalysisStatusChangedConsumer is now listening on queue '{QueueName}'", QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("AIAnalysisStatusChangedConsumer is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AIAnalysisStatusChangedConsumer encountered an error. It will not retry.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
