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
using UavPms.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;
using UavPms.Core.Contracts;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Infrastructure.Messaging;

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

    private IConnection? _connection;
    private IChannel? _channel;

    private const string ExchangeName = "identity-exchange";
    private const string DeadLetterExchangeName = "ai.analysis.dlx";
    private const string QueueName = "ai.analysis.server.requested";
    private const string RoutingKey = "identity.event.aianalysisrequestedevent.server";

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

            await _channel.ExchangeDeclareAsync(
                exchange: DeadLetterExchangeName,
                type: ExchangeType.Fanout,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await _channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = DeadLetterExchangeName
                },
                cancellationToken: stoppingToken);

            await _channel.QueueBindAsync(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: RoutingKey,
                cancellationToken: stoppingToken);

            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) => await ProcessMessageAsync(ea, stoppingToken);

            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogWarning("MockAIAnalysisConsumer is listening on queue '{QueueName}'", QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MockAIAnalysisConsumer is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MockAIAnalysisConsumer encountered an error. It will not retry until the host restarts.");
        }
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        if (_channel == null)
        {
            return;
        }

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
                await _channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
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

                await _channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
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
                "Mock AI callback processed. RequestId={RequestId}, SavedDetections={SavedDetections}, CreatedAlerts={CreatedAlerts}",
                result.RequestId,
                result.SavedDetections,
                result.CreatedAlerts);

            await _channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mock AI failed to process message.");
            await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, cancellationToken);
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
