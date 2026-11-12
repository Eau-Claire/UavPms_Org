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
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private readonly ILogger<AIAnalysisStatusChangedConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

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

                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing AIAnalysisStatusChangedEvent");
                        await channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("AIAnalysisStatusChangedConsumer is now listening on queue '{QueueName}'", QueueName);

                await disconnected.Task.WaitAsync(stoppingToken);
                _logger.LogWarning("AIAnalysisStatusChangedConsumer RabbitMQ connection lost; reconnecting.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("AIAnalysisStatusChangedConsumer is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AIAnalysisStatusChangedConsumer disconnected; retrying in {DelaySeconds}s.", ReconnectDelay.TotalSeconds);
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
