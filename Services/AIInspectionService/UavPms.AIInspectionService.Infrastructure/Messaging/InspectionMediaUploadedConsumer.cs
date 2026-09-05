using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.StartUploadedMediaAnalysis;
using UavPms.Shared.Contracts.Events;

namespace UavPms.AIInspectionService.Infrastructure.Messaging;

/// <summary>Starts Cloud AI analysis from the single authoritative Operations upload event.</summary>
public sealed class InspectionMediaUploadedConsumer : BackgroundService
{
    public const string QueueName = "ai.inspection-media.uploaded";
    public const string RoutingKey = "identity.event.inspectionmediauploadedevent";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private readonly RabbitMqConnection _connectionFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InspectionMediaUploadedConsumer> _logger;

    public InspectionMediaUploadedConsumer(
        RabbitMqConnection connectionFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<InspectionMediaUploadedConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.ExchangeDeclareAsync(AIAnalysisRequestTopology.ExchangeName, ExchangeType.Topic, true, false, cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(QueueName, true, false, false,
                    new Dictionary<string, object?> { ["x-dead-letter-exchange"] = AIAnalysisRequestTopology.DeadLetterExchangeName },
                    cancellationToken: stoppingToken);
                await channel.QueueBindAsync(QueueName, AIAnalysisRequestTopology.ExchangeName, RoutingKey, cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);

                var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };
                channel.ChannelShutdownAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        var upload = JsonSerializer.Deserialize<InspectionMediaUploadedEvent>(delivery.Body.Span,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (upload == null || upload.EventId == Guid.Empty || upload.MediaId == Guid.Empty ||
                            upload.MissionId == Guid.Empty || upload.AssetId == Guid.Empty || upload.UploadedBy == Guid.Empty)
                            throw new JsonException("Inspection media event is missing required identifiers.");

                        using var scope = _scopeFactory.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<ISender>()
                            .Send(new StartUploadedMediaAnalysisCommand(upload), stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to start AI analysis from inspection media event.");
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, requeue: false, stoppingToken);
                    }
                };
                await channel.BasicConsumeAsync(QueueName, false, consumer, stoppingToken);
                await disconnected.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Inspection media consumer disconnected; retrying.");
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }
}
