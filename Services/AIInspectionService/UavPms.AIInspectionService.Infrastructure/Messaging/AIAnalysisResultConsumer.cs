using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;
using UavPms.AIInspectionService.Domain.Contracts;

namespace UavPms.AIInspectionService.Infrastructure.Messaging;

public class AIAnalysisResultConsumer : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<AIAnalysisResultConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AIAnalysisResultMessagingOptions _options;

    public AIAnalysisResultConsumer(
        ILogger<AIAnalysisResultConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory,
        IOptions<AIAnalysisResultMessagingOptions> options)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AIAnalysisResultConsumer starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Connecting to RabbitMQ");
                await using var connection = await _rabbitMqConnection.CreateConnectionAsync(stoppingToken);
                _logger.LogInformation("RabbitMQ connection established");

                var channelOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);
                await using var channel = await connection.CreateChannelAsync(channelOptions, stoppingToken);
                await channel.BasicQosAsync(0, _options.PrefetchCount, false, stoppingToken);
                await DeclareTopologyAsync(channel, stoppingToken);

                var connectionLost = new TaskCompletionSource<ShutdownEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, args) =>
                {
                    connectionLost.TrySetResult(args);
                    return Task.CompletedTask;
                };
                channel.ChannelShutdownAsync += (_, args) =>
                {
                    connectionLost.TrySetResult(args);
                    return Task.CompletedTask;
                };

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, ea) => HandleMessageAsync(ea, channel, stoppingToken);

                await channel.BasicConsumeAsync(
                    queue: _options.QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("Consumer started on queue {QueueName}", _options.QueueName);
                var shutdown = await connectionLost.Task.WaitAsync(stoppingToken);
                _logger.LogWarning(
                    "RabbitMQ connection lost. ReplyCode={ReplyCode}, ReplyText={ReplyText}",
                    shutdown.ReplyCode,
                    shutdown.ReplyText);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "RabbitMQ unavailable. ExceptionType={ExceptionType}, ErrorMessage={ErrorMessage}",
                    ex.GetType().Name,
                    ex.Message);
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Consumer reconnecting");
                _logger.LogWarning(
                    "RabbitMQ unavailable; retrying in {RetryDelaySeconds} seconds",
                    ReconnectDelay.TotalSeconds);
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

        _logger.LogInformation("Consumer stopped");
    }

    public async Task HandleMessageAsync(BasicDeliverEventArgs ea, IChannel channel, CancellationToken cancellationToken)
    {
        var retryCount = GetRetryCount(ea.BasicProperties);

        try
        {
            var payload = JsonSerializer.Deserialize<AIAnalysisResultEvent>(ea.Body.Span, JsonOptions);
            if (payload == null || payload.AnalysisId == Guid.Empty || payload.EventId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Status))
            {
                throw new JsonException("AI analysis result event is missing required fields.");
            }

            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
            var command = MapToCommand(payload);
            await mediator.Send(command, cancellationToken);

            await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid AI analysis result payload. DeliveryTag={DeliveryTag}", ea.DeliveryTag);
            await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, cancellationToken);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "AI analysis result references missing entity. DeliveryTag={DeliveryTag}", ea.DeliveryTag);
            await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, cancellationToken);
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "AI analysis result violates business rules. DeliveryTag={DeliveryTag}", ea.DeliveryTag);
            await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, cancellationToken);
        }
        catch (Exception ex) when (IsTransient(ex) && retryCount < _options.RetryLimit)
        {
            _logger.LogWarning(ex, "Transient error processing AI analysis result. Retrying attempt {Attempt}/{Limit}.",
                retryCount + 1, _options.RetryLimit);
            await RepublishToRetryQueueAsync(channel, ea, retryCount + 1, cancellationToken);
            await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process AI analysis result. DeliveryTag={DeliveryTag}", ea.DeliveryTag);
            await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, cancellationToken);
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(_options.DeadLetterExchangeName, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(_options.DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(_options.DeadLetterQueueName, _options.DeadLetterExchangeName, routingKey: string.Empty, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _options.DeadLetterExchangeName
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, _options.RoutingKey, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            _options.RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = _options.RetryDelayMs,
                ["x-dead-letter-exchange"] = _options.ExchangeName,
                ["x-dead-letter-routing-key"] = _options.RoutingKey
            },
            cancellationToken: cancellationToken);
    }

    private async Task RepublishToRetryQueueAsync(IChannel channel, BasicDeliverEventArgs ea, int retryCount, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, object?>(ea.BasicProperties.Headers ?? new Dictionary<string, object?>())
        {
            ["x-retry-count"] = retryCount
        };

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = ea.BasicProperties.ContentType ?? "application/json",
            CorrelationId = ea.BasicProperties.CorrelationId,
            MessageId = ea.BasicProperties.MessageId,
            Type = ea.BasicProperties.Type,
            Headers = headers
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _options.RetryQueueName,
            mandatory: false,
            basicProperties: props,
            body: ea.Body,
            cancellationToken: cancellationToken);
    }

    private static bool IsTransient(Exception ex)
        => ex is TimeoutException
        || ex is DbUpdateException
        || ex.InnerException is TimeoutException
        || ex.InnerException is DbUpdateException;

    private static int GetRetryCount(IReadOnlyBasicProperties? properties)
    {
        if (properties?.Headers == null || !properties.Headers.TryGetValue("x-retry-count", out var raw) || raw == null)
        {
            return 0;
        }

        return raw switch
        {
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            sbyte sb => sb,
            byte b => b,
            short s => s,
            int i => i,
            long l => (int)l,
            _ => 0
        };
    }

    private static ProcessAiAnalysisResultCommand MapToCommand(AIAnalysisResultEvent payload)
        => new()
        {
            RequestId = payload.AnalysisId,
            MediaId = payload.MediaId,
            MissionId = payload.MissionId,
            AssetId = payload.AssetId,
            Status = payload.Status,
            ModelName = payload.ModelName,
            ModelVersion = payload.ModelVersion,
            ProcessingTimeMs = payload.ProcessingTimeMs,
            Detections = payload.Results.Select(d => new DetectionDto
            {
                Id = d.Id,
                CategoryCode = d.CategoryCode,
                ClassName = d.Class,
                Confidence = d.Confidence,
                BoundingBox = new BoundingBoxDto
                {
                    X = d.BoundingBox.X,
                    Y = d.BoundingBox.Y,
                    Width = d.BoundingBox.Width,
                    Height = d.BoundingBox.Height
                },
                FrameIndex = d.FrameIndex,
                Timestamp = d.Timestamp,
                TimestampMs = d.TimestampMs,
                ImageUrl = d.ImageUrl,
                CropUrl = d.CropUrl,
                Gps = d.Gps == null ? null : new GpsDto { Lat = d.Gps.Lat, Lng = d.Gps.Lng },
                TowerId = d.TowerId,
                AssetId = d.AssetId
            }).ToList(),
            VideoMetadata = payload.VideoMetadata == null ? null : new VideoMetadataDto
            {
                Duration = payload.VideoMetadata.Duration,
                Fps = payload.VideoMetadata.Fps,
                Width = payload.VideoMetadata.Width,
                Height = payload.VideoMetadata.Height
            },
            RawResult = payload.RawResult,
            ErrorCode = payload.ErrorCode,
            ErrorMessage = payload.ErrorMessage,
            CompletedAt = payload.ProcessedAt
        };
}
