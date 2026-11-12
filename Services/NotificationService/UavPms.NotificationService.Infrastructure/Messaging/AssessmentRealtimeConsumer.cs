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

public class AssessmentRealtimeConsumer : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private readonly ILogger<AssessmentRealtimeConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    private const string QueueName = "notification.assessment-realtime";
    private const string RoutingKey = "identity.event.assessmentrealtimeintegrationevent";

    public AssessmentRealtimeConsumer(
        ILogger<AssessmentRealtimeConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AssessmentRealtimeConsumer is starting...");

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
                        var realtimeEvent = JsonSerializer.Deserialize<AssessmentRealtimeIntegrationEvent>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (realtimeEvent?.Event != null && !string.IsNullOrWhiteSpace(realtimeEvent.Event.AssessmentId))
                        {
                            var evt = realtimeEvent.Event;
                            _logger.LogInformation("Received AssessmentRealtimeIntegrationEvent for assessment {AssessmentId}, type {Type}", evt.AssessmentId, evt.Type);

                            using var scope = _scopeFactory.CreateScope();
                            var realtimeService = scope.ServiceProvider.GetRequiredService<IRealtimeNotificationService>();
                            await realtimeService.SendAssessmentEventAsync(evt, stoppingToken);
                        }

                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing AssessmentRealtimeIntegrationEvent");
                        await channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("AssessmentRealtimeConsumer is now listening on queue '{QueueName}'", QueueName);

                await disconnected.Task.WaitAsync(stoppingToken);
                _logger.LogWarning("AssessmentRealtimeConsumer RabbitMQ connection lost; reconnecting.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("AssessmentRealtimeConsumer is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AssessmentRealtimeConsumer disconnected; retrying in {DelaySeconds}s.", ReconnectDelay.TotalSeconds);
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
