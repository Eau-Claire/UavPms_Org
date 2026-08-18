using RabbitMQ.Client;

namespace UavPms.AIInspectionService.Infrastructure.Messaging;

public static class AIAnalysisRequestTopology
{
    public const string ExchangeName = "identity-exchange";
    public const string DeadLetterExchangeName = "ai.analysis.dlx";
    public const string ServerQueueName = "ai.analysis.server.requested";
    public const string ServerImageQueueName = "ai.analysis.server.image.requested";
    public const string ServerVideoQueueName = "ai.analysis.server.video.requested";
    public const string EdgeQueueName = "ai.analysis.edge.requested";
    public const string EdgeImageQueueName = "ai.analysis.edge.image.requested";
    public const string EdgeVideoQueueName = "ai.analysis.edge.video.requested";
    public const string ServerRoutingKey = "identity.event.aianalysisrequestedevent.server";
    public const string ServerImageRoutingKey = "identity.event.aianalysisrequestedevent.server.image";
    public const string ServerVideoRoutingKey = "identity.event.aianalysisrequestedevent.server.video";
    public const string EdgeRoutingKey = "identity.event.aianalysisrequestedevent.edge";
    public const string EdgeImageRoutingKey = "identity.event.aianalysisrequestedevent.edge.image";
    public const string EdgeVideoRoutingKey = "identity.event.aianalysisrequestedevent.edge.video";

    public static readonly IReadOnlyList<AIAnalysisRequestRoute> Routes =
    [
        new(ServerQueueName, ServerRoutingKey),
        new(ServerImageQueueName, ServerImageRoutingKey),
        new(ServerVideoQueueName, ServerVideoRoutingKey),
        new(EdgeQueueName, EdgeRoutingKey),
        new(EdgeImageQueueName, EdgeImageRoutingKey),
        new(EdgeVideoQueueName, EdgeVideoRoutingKey)
    ];

    public static string GetRoutingKey(string? preferredModel, string? mediaType)
    {
        var runtime = string.Equals(preferredModel, "YOLO11", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(preferredModel, "EDGE", StringComparison.OrdinalIgnoreCase)
            ? "edge"
            : "server";
        var media = string.Equals(mediaType, "Video", StringComparison.OrdinalIgnoreCase)
            ? "video"
            : "image";

        return $"identity.event.aianalysisrequestedevent.{runtime}.{media}";
    }

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            DeadLetterExchangeName,
            ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        foreach (var route in Routes)
        {
            await channel.QueueDeclareAsync(
                route.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = DeadLetterExchangeName
                },
                cancellationToken: cancellationToken);
            await channel.QueueBindAsync(
                route.QueueName,
                ExchangeName,
                route.RoutingKey,
                cancellationToken: cancellationToken);
        }
    }
}

public sealed record AIAnalysisRequestRoute(string QueueName, string RoutingKey);
