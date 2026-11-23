using System;
using System.Buffers;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using UavPms.OperationsService.Application.Common.Options;
using UavPms.OperationsService.Application.Features.Telemetry.DTOs;

namespace UavPms.OperationsService.Infrastructure.Messaging;

public sealed class MqttDroneConsumer : BackgroundService
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
        if (!_options.Enabled)
        {
            _logger.LogInformation("MqttDroneConsumer is disabled (Mqtt:Enabled=false).");
            return;
        }

        _client.ApplicationMessageReceivedAsync += HandleMessageAsync;
        _client.DisconnectedAsync += args =>
        {
            _logger.LogWarning(
                args.Exception,
                "MQTT client disconnected from {Host}:{Port} (Reason={Reason}). Will reconnect with backoff.",
                _options.Host, _options.Port, args.Reason);
            return Task.CompletedTask;
        };

        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    await _client.ConnectAsync(BuildClientOptions(), stoppingToken);
                    await SubscribeAsync(stoppingToken);
                    attempt = 0;
                    _logger.LogInformation(
                        "MQTT drone consumer connected to {Host}:{Port} (TLS={UseTls}) and subscribed to '{Topic}' with QoS 1.",
                        _options.Host, _options.Port, _options.UseTls, _options.TelemetryTopic);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                attempt++;
                var backoffSeconds = Math.Min(60, (int)Math.Pow(2, Math.Min(attempt, 6)));
                _logger.LogWarning(
                    ex,
                    "MQTT drone consumer connection attempt #{Attempt} to {Host}:{Port} failed. Retrying in {BackoffSeconds}s.",
                    attempt, _options.Host, _options.Port, backoffSeconds);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            try
            {
                await _client.DisconnectAsync(cancellationToken: cancellationToken);
                _logger.LogInformation("MQTT drone consumer disconnected cleanly.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while disconnecting MQTT drone consumer.");
            }
        }

        await base.StopAsync(cancellationToken);
    }

    internal MqttClientOptions BuildClientOptions()
    {
        var clientId = string.IsNullOrWhiteSpace(_options.ClientId)
            ? $"uav-pms-operations-{Guid.NewGuid():N}"
            : $"{_options.ClientId}-{Environment.MachineName}";

        var builder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(_options.Host, _options.Port)
            .WithCleanSession();

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            builder.WithCredentials(_options.Username, _options.Password);
        }

        if (_options.UseTls)
        {
            builder.WithTlsOptions(tls =>
            {
                tls.UseTls();
                tls.WithSslProtocols(SslProtocols.Tls12 | SslProtocols.Tls13);
            });
        }

        return builder.Build();
    }

    private async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f =>
            {
                f.WithTopic(_options.TelemetryTopic);
                f.WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);
            })
            .Build();

        await _client.SubscribeAsync(subscribeOptions, cancellationToken);
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic ?? string.Empty;
        try
        {
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload.ToArray());
            await using var scope = _scopeFactory.CreateAsyncScope();
            var telemetryService = scope.ServiceProvider.GetRequiredService<IDroneTelemetryService>();
            await telemetryService.IngestAsync(topic, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message on topic '{Topic}'.", topic);
        }
    }
}
