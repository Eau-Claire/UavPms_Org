using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace UavPms.AIInspectionService.Infrastructure.Messaging;

public sealed class AIAnalysisRequestTopologyInitializer : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly ILogger<AIAnalysisRequestTopologyInitializer> _logger;

    public AIAnalysisRequestTopologyInitializer(
        RabbitMqConnection rabbitMqConnection,
        ILogger<AIAnalysisRequestTopologyInitializer> logger)
    {
        _rabbitMqConnection = rabbitMqConnection;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Hosted services start before ASP.NET accepts requests. Awaiting the first declaration
        // closes the startup race where a publish could happen before durable bindings exist.
        await using var connection = await _rabbitMqConnection.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await AIAnalysisRequestTopology.DeclareAsync(channel, cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await _rabbitMqConnection.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await AIAnalysisRequestTopology.DeclareAsync(channel, stoppingToken);

                _logger.LogInformation(
                    "Declared {RouteCount} AI analysis request queue bindings on exchange {ExchangeName}",
                    AIAnalysisRequestTopology.Routes.Count,
                    AIAnalysisRequestTopology.ExchangeName);

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
                await disconnected.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not declare AI request topology; retrying in {DelaySeconds} seconds",
                    ReconnectDelay.TotalSeconds);
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }
}
