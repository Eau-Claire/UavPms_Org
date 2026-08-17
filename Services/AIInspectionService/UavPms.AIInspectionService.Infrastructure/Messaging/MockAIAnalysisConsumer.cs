using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;

namespace UavPms.AIInspectionService.Infrastructure.Messaging;

/// <summary>
/// Temporary demo-only AI worker. It consumes AIAnalysisRequestedEvent messages and creates
/// deterministic mock detections through the same callback command used by the real AI service.
/// Enable with MockAI:Enabled=true. Disable when a real AI worker is available.
/// </summary>
public class MockAIAnalysisConsumer : BackgroundService
{
    private readonly ILogger<MockAIAnalysisConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    private const string ExchangeName = "identity-exchange";
    private const string DeadLetterExchangeName = "ai.analysis.dlx";
    private const string ImageQueueName = "ai.analysis.server.image.requested";
    private const string VideoQueueName = "ai.analysis.server.video.requested";
    private const string ImageRoutingKey = "identity.event.aianalysisrequestedevent.server.image";
    private const string VideoRoutingKey = "identity.event.aianalysisrequestedevent.server.video";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    public MockAIAnalysisConsumer(
        ILogger<MockAIAnalysisConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning("MockAIAnalysisConsumer is ENABLED. This is for demo/testing only and must be disabled when a real AI worker is running.");

        var imageConcurrency = GetPositiveInt("MockAI:ImageConcurrency", 2);
        var videoConcurrency = GetPositiveInt("MockAI:VideoConcurrency", 1);
        var consumers = new List<Task>(imageConcurrency + videoConcurrency);

        for (var i = 0; i < imageConcurrency; i++)
        {
            consumers.Add(ConsumeQueueAsync(ImageQueueName, ImageRoutingKey, "image", i + 1, stoppingToken));
        }

        for (var i = 0; i < videoConcurrency; i++)
        {
            consumers.Add(ConsumeQueueAsync(VideoQueueName, VideoRoutingKey, "video", i + 1, stoppingToken));
        }

        _logger.LogWarning(
            "Mock AI started {ImageConcurrency} image worker(s) and {VideoConcurrency} video worker(s)",
            imageConcurrency,
            videoConcurrency);

        await Task.WhenAll(consumers);
    }

    private async Task ConsumeQueueAsync(
        string queueName,
        string routingKey,
        string mediaType,
        int workerNumber,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await _rabbitMqConnection.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.ExchangeDeclareAsync(
                    exchange: ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.ExchangeDeclareAsync(
                    exchange: DeadLetterExchangeName,
                    type: ExchangeType.Fanout,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await DeclareQueueAsync(channel, queueName, routingKey, stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);

                var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, _) =>
                {
                    disconnected.TrySetResult();
                    return Task.CompletedTask;
                };
                channel.ChannelShutdownAsync += (_, _) =>
                {
                    disconnected.TrySetResult();
                    return Task.CompletedTask;
                };

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, ea) => ProcessMessageAsync(ea, channel, stoppingToken);

                await channel.BasicConsumeAsync(queueName, false, consumer, stoppingToken);
                _logger.LogInformation(
                    "Mock AI {MediaType} worker {WorkerNumber} is listening on '{QueueName}'",
                    mediaType,
                    workerNumber,
                    queueName);

                await disconnected.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Mock AI {MediaType} worker {WorkerNumber} disconnected; retrying in {DelaySeconds} seconds",
                    mediaType,
                    workerNumber,
                    ReconnectDelay.TotalSeconds);
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private static async Task DeclareQueueAsync(IChannel channel, string queueName, string routingKey, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = DeadLetterExchangeName
                },
                cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
                queue: queueName,
                exchange: ExchangeName,
                routingKey: routingKey,
                cancellationToken: cancellationToken);
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs ea, IChannel channel, CancellationToken cancellationToken)
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var aiRequestEvent = JsonSerializer.Deserialize<AIAnalysisRequestedEvent>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (aiRequestEvent == null)
            {
                _logger.LogWarning("Mock AI received invalid message: {Message}", json);
                await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
                return;
            }

            _logger.LogWarning(
                "Mock AI processing request. RequestId={RequestId}, MediaId={MediaId}, MissionId={MissionId}, FileUrl={FileUrl}",
                aiRequestEvent.RequestId,
                aiRequestEvent.MediaId,
                aiRequestEvent.MissionId,
                aiRequestEvent.FileUrl);

            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
            var categoryRepository = scope.ServiceProvider.GetRequiredService<IGenericRepository<DefectCategory>>();

            var categoryCode = await ResolveCategoryCodeAsync(categoryRepository);
            if (string.IsNullOrWhiteSpace(categoryCode))
            {
                await mediator.Send(new ProcessAiAnalysisResultCommand
                {
                    RequestId = aiRequestEvent.RequestId,
                    MediaId = aiRequestEvent.MediaId,
                    Status = "Failed",
                    ErrorCode = "MOCK_AI_NO_CATEGORY",
                    ErrorMessage = "Mock AI could not find any DefectCategory to use for demo detection.",
                    CompletedAt = DateTime.UtcNow
                }, cancellationToken);

                await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
                return;
            }

            var confidence = GetDouble("MockAI:Confidence", 0.92);
            var command = new ProcessAiAnalysisResultCommand
            {
                RequestId = aiRequestEvent.RequestId,
                MediaId = aiRequestEvent.MediaId,
                Status = "Completed",
                ModelName = _configuration["MockAI:ModelName"] ?? "MockAI",
                ModelVersion = _configuration["MockAI:ModelVersion"] ?? "demo",
                ProcessingTimeMs = GetInt("MockAI:ProcessingTimeMs", 350),
                CompletedAt = DateTime.UtcNow,
                RawResult = new
                {
                    mocked = true,
                    source = nameof(MockAIAnalysisConsumer)
                },
                Detections =
                [
                    new DetectionDto
                    {
                        CategoryCode = categoryCode,
                        Confidence = confidence,
                        BoundingBox = new BoundingBoxDto
                        {
                            X = GetDouble("MockAI:BoundingBox:X", 0.15),
                            Y = GetDouble("MockAI:BoundingBox:Y", 0.18),
                            Width = GetDouble("MockAI:BoundingBox:Width", 0.32),
                            Height = GetDouble("MockAI:BoundingBox:Height", 0.38)
                        }
                    }
                ]
            };

            var result = await mediator.Send(command, cancellationToken);

            _logger.LogWarning(
                "Mock AI result processed. RequestId={RequestId}, SavedDetections={SavedDetections}, CreatedAlerts={CreatedAlerts}",
                result.RequestId,
                result.SavedDetections,
                result.CreatedAlerts);

            await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mock AI failed to process message.");
            await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, cancellationToken);
        }
    }

    private async Task<string?> ResolveCategoryCodeAsync(IGenericRepository<DefectCategory> categoryRepository)
    {
        var configuredCategoryCode = _configuration["MockAI:CategoryCode"];
        if (!string.IsNullOrWhiteSpace(configuredCategoryCode))
        {
            var configuredMatches = await categoryRepository.FindAsync(
                category => category.CategoryCode == configuredCategoryCode,
                track: false);

            var configuredCategory = configuredMatches.FirstOrDefault();
            if (configuredCategory != null)
            {
                return configuredCategory.CategoryCode;
            }

            _logger.LogWarning("Configured MockAI category code does not exist: {CategoryCode}. Falling back to first category.", configuredCategoryCode);
        }

        var categories = await categoryRepository.GetAllAsync(track: false);
        return categories
            .OrderByDescending(category => category.IsEmergencyClass)
            .ThenByDescending(category => category.SeverityWeight)
            .ThenBy(category => category.Id)
            .FirstOrDefault()
            ?.CategoryCode;
    }

    private double GetDouble(string key, double fallback)
    {
        return double.TryParse(_configuration[key], out var value) ? value : fallback;
    }

    private int GetInt(string key, int fallback)
    {
        return int.TryParse(_configuration[key], out var value) ? value : fallback;
    }

    private int GetPositiveInt(string key, int fallback)
    {
        return int.TryParse(_configuration[key], out var value) && value > 0 ? value : fallback;
    }
}
