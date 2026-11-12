using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UavPms.Shared.Contracts.Events;
using UavPms.NotificationService.Application.Features.Notifications.Commands.CreateNotification;

namespace UavPms.NotificationService.Infrastructure.Messaging;

public class MissionCreatedConsumer : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private readonly ILogger<MissionCreatedConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    private const string QueueName = "notification.mission-created";
    private const string RoutingKey = "identity.event.missioncreatedevent";
    private const string AssessmentRoutingKey = "identity.event.missioncreatedfromassessmentevent";

    public MissionCreatedConsumer(
        ILogger<MissionCreatedConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MissionCreatedConsumer is starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await _rabbitMqConnection.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await RabbitMqExchangeBinding.DeclareAndBindDualExchangeAsync(
                    channel,
                    QueueName,
                    stoppingToken,
                    RoutingKey,
                    AssessmentRoutingKey);

                var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };
                channel.ChannelShutdownAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var json = Encoding.UTF8.GetString(body);

                        if (ea.RoutingKey.Equals(AssessmentRoutingKey, StringComparison.OrdinalIgnoreCase))
                        {
                            var assessmentEvent = JsonSerializer.Deserialize<MissionCreatedFromAssessmentEvent>(json);
                            if (assessmentEvent != null)
                            {
                                _logger.LogInformation("Received MissionCreatedFromAssessmentEvent for mission {MissionId}", assessmentEvent.MissionId);

                                using var scope = _scopeFactory.CreateScope();
                                var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

                                if (assessmentEvent.ManagerId != Guid.Empty)
                                {
                                    await mediator.Send(new CreateNotificationCommand(
                                        assessmentEvent.ManagerId,
                                        "MissionCreated",
                                        "Mission",
                                        assessmentEvent.MissionId,
                                        "Nhiệm vụ mới đã tạo từ thẩm định",
                                        $"Nhiệm vụ '{assessmentEvent.Title}' đã được tạo từ thẩm định {assessmentEvent.AssessmentId} và phân công thành công."
                                    ), stoppingToken);
                                }
                            }
                        }
                        else
                        {
                            var missionEvent = JsonSerializer.Deserialize<MissionCreatedEvent>(json);

                            if (missionEvent != null)
                            {
                                _logger.LogInformation("Received MissionCreatedEvent for mission {MissionId}", missionEvent.MissionId);

                                using var scope = _scopeFactory.CreateScope();
                                var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

                                // Notify the assigned inspector
                                await mediator.Send(new CreateNotificationCommand(
                                    missionEvent.AssignedToUserId,
                                    "MissionAssigned",
                                    "Mission",
                                    missionEvent.MissionId,
                                    "Nhiệm vụ mới được giao",
                                    $"Bạn đã được giao nhiệm vụ bay kiểm tra '{missionEvent.MissionCode}'. {missionEvent.Description}"
                                ), stoppingToken);

                                // Notify the manager
                                if (missionEvent.ManagerId != Guid.Empty && missionEvent.ManagerId != missionEvent.AssignedToUserId)
                                {
                                    await mediator.Send(new CreateNotificationCommand(
                                        missionEvent.ManagerId,
                                        "MissionCreated",
                                        "Mission",
                                        missionEvent.MissionId,
                                        "Nhiệm vụ mới đã tạo",
                                        $"Nhiệm vụ '{missionEvent.MissionCode}' đã được tạo và phân công thành công."
                                    ), stoppingToken);
                                }
                            }
                        }

                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing MissionCreatedEvent / MissionCreatedFromAssessmentEvent");
                        await channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("MissionCreatedConsumer is now listening on queue '{QueueName}'", QueueName);

                await disconnected.Task.WaitAsync(stoppingToken);
                _logger.LogWarning("MissionCreatedConsumer RabbitMQ connection lost; reconnecting.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("MissionCreatedConsumer is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MissionCreatedConsumer disconnected; retrying in {DelaySeconds}s.", ReconnectDelay.TotalSeconds);
            }

            if (!stoppingToken.IsCancellationRequested)
            {
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
    }
}
