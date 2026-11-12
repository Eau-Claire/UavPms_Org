using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using UavPms.Shared.Contracts.Events;

namespace UavPms.NotificationService.Infrastructure.Messaging;

/// <summary>
/// Declares the primary and legacy topic exchanges and binds a durable queue to both
/// so NotificationService receives events regardless of which exchange a publisher targets.
/// </summary>
public static class RabbitMqExchangeBinding
{
    public static async Task DeclareAndBindDualExchangeAsync(
        IChannel channel,
        string queueName,
        CancellationToken cancellationToken,
        params string[] routingKeys)
    {
        await channel.ExchangeDeclareAsync(
            exchange: EventTopologyConstants.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: EventTopologyConstants.LegacyExchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        foreach (var routingKey in routingKeys)
        {
            await channel.QueueBindAsync(
                queue: queueName,
                exchange: EventTopologyConstants.Exchange,
                routingKey: routingKey,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: queueName,
                exchange: EventTopologyConstants.LegacyExchange,
                routingKey: routingKey,
                cancellationToken: cancellationToken);
        }
    }
}
