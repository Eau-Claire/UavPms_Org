using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using UavPms.Core.Contracts;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Infrastructure.Messaging;

public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    public RabbitMqEventPublisher(RabbitMqConnection rabbitMqConnection, ILogger<RabbitMqEventPublisher> logger)
    {
        _rabbitMqConnection = rabbitMqConnection;
        _logger = logger;
    }

    private static string GetRoutingKey<T>(string eventName, T @event) where T : class
    {
        var baseRoutingKey = $"identity.event.{eventName.ToLowerInvariant()}";
        if (@event is AIAnalysisRequestedEvent aiAnalysisRequestedEvent)
        {
            return $"{baseRoutingKey}.{ResolveAiRuntime(aiAnalysisRequestedEvent.PreferredModel)}";
        }

        return baseRoutingKey;
    }

    private static string ResolveAiRuntime(string? preferredModel)
    {
        if (string.Equals(preferredModel, "YOLO11", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(preferredModel, "EDGE", StringComparison.OrdinalIgnoreCase))
        {
            return "edge";
        }

        return "server";
    }

    public async Task PublishAsync<T>(T @event) where T : class
    {
        try
        {
            using var connection = await _rabbitMqConnection.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            var exchangeName = "identity-exchange";
            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null);

            var eventName = @event.GetType().Name;
            var routingKey = GetRoutingKey(eventName, @event);

            var json = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(json);

            // In RabbitMQ.Client 7.x, we use BasicPublishAsync
            await channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey,
                body: body);

            _logger.LogInformation("Published event {EventName} to exchange {ExchangeName} with routing key {RoutingKey}", 
                eventName, exchangeName, routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventName} to RabbitMQ", @event.GetType().Name);
            throw;
        }
    }
}
