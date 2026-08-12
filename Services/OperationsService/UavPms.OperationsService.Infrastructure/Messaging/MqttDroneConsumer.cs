using System.Buffers;
using System.Text;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using UavPms.OperationsService.Application.Common.Options;
using UavPms.OperationsService.Application.Features.Drones.Commands.ProcessDroneStatus;
using UavPms.OperationsService.Application.Features.Drones.Commands.ProcessDroneTelemetry;

namespace UavPms.OperationsService.Infrastructure.Messaging;

public class MqttDroneConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttOptions _options;
    private readonly ILogger<MqttDroneConsumer> _logger;
    private readonly IMqttClient _client;

    public MqttDroneConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<MqttOptions> options,
        ILogger<MqttDroneConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _client = new MqttClientFactory().CreateMqttClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.ApplicationMessageReceivedAsync += HandleMessageAsync;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    await _client.ConnectAsync(BuildClientOptions(), stoppingToken);
                    await SubscribeAsync(stoppingToken);
                    _logger.LogInformation("MQTT drone consumer connected to {Host}:{Port}", _options.Host, _options.Port);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MQTT drone consumer connection failed. Retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(cancellationToken: cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }

    private MqttClientOptions BuildClientOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithClientId(_options.ClientId)
            .WithTcpServer(_options.Host, _options.Port)
            .WithCleanSession();

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            builder.WithCredentials(_options.Username, _options.Password);
        }

        return builder.Build();
    }

    private async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(_options.StatusTopic)
            .WithTopicFilter(_options.TelemetryTopic)
            .Build();

        await _client.SubscribeAsync(subscribeOptions, cancellationToken);
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload.ToArray());

            await using var scope = _scopeFactory.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

            if (MqttDroneMessageParser.TryGetTopicDroneCode(topic, "status", out var statusDroneCode))
            {
                if (!MqttDroneMessageParser.TryParseStatus(payload, out var status))
                {
                    _logger.LogWarning("Invalid MQTT drone status payload on topic {Topic}", topic);
                    return;
                }

                await mediator.Send(new ProcessDroneStatusCommand(
                    statusDroneCode,
                    status!.DroneCode,
                    status.Status,
                    status.Battery,
                    status.Timestamp));
                return;
            }

            if (MqttDroneMessageParser.TryGetTopicDroneCode(topic, "telemetry", out var telemetryDroneCode))
            {
                if (!MqttDroneMessageParser.TryParseTelemetry(payload, out var telemetry))
                {
                    _logger.LogWarning("Invalid MQTT drone telemetry payload on topic {Topic}", topic);
                    return;
                }

                await mediator.Send(new ProcessDroneTelemetryCommand(
                    telemetryDroneCode,
                    telemetry!.DroneCode,
                    telemetry.Timestamp,
                    telemetry.Latitude,
                    telemetry.Longitude,
                    telemetry.Altitude,
                    telemetry.Battery,
                    telemetry.Speed,
                    telemetry.Heading));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MQTT drone message handling failed");
        }
    }
}
