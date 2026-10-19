using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Events;

namespace UavPms.OperationsService.Infrastructure.Messaging;

public class AiDetectionCompletedConsumer : BackgroundService
{
    private readonly ILogger<AiDetectionCompletedConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    public AiDetectionCompletedConsumer(
        ILogger<AiDetectionCompletedConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AiDetectionCompletedConsumer is starting...");

        try
        {
            _connection = await _rabbitMqConnection.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await _channel.ExchangeDeclareAsync(
                exchange: EventTopologyConstants.Exchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await _channel.QueueDeclareAsync(
                queue: EventTopologyConstants.Queues.OperationsDefectRecord,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await _channel.QueueBindAsync(
                queue: EventTopologyConstants.Queues.OperationsDefectRecord,
                exchange: EventTopologyConstants.Exchange,
                routingKey: EventTopologyConstants.RoutingKeys.AiDetectionCompleted,
                cancellationToken: stoppingToken);

            await _channel.QueueBindAsync(
                queue: EventTopologyConstants.Queues.OperationsDefectRecord,
                exchange: EventTopologyConstants.Exchange,
                routingKey: EventTopologyConstants.RoutingKeys.LegacyDefectDetected,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    AiDetectionCompletedEvent? detectionEvent = null;
                    if (ea.RoutingKey.Equals(EventTopologyConstants.RoutingKeys.LegacyDefectDetected, StringComparison.OrdinalIgnoreCase))
                    {
                        var legacy = JsonSerializer.Deserialize<DefectDetectedEvent>(json);
                        if (legacy != null)
                        {
                            detectionEvent = new AiDetectionCompletedEvent
                            {
                                EventId = Guid.NewGuid(),
                                InspectionId = legacy.InspectionId,
                                MissionId = legacy.MissionId,
                                AnomalyId = legacy.RecordId,
                                DefectType = legacy.DefectType,
                                IsDefect = legacy.IsDefect,
                                ImageUrl = legacy.ImageUrl,
                                DetectedAt = legacy.DetectedAt
                            };
                        }
                    }
                    else
                    {
                        detectionEvent = JsonSerializer.Deserialize<AiDetectionCompletedEvent>(json);
                    }

                    if (detectionEvent != null && detectionEvent.IsDefect)
                    {
                        await ProcessDefectRecordAsync(db, detectionEvent, stoppingToken);
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing AI detection in OperationsService");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: EventTopologyConstants.Queues.OperationsDefectRecord,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("AiDetectionCompletedConsumer listening on queue '{QueueName}'",
                EventTopologyConstants.Queues.OperationsDefectRecord);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("AiDetectionCompletedConsumer is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiDetectionCompletedConsumer encountered an error. It will not retry.");
        }
    }

    private async Task ProcessDefectRecordAsync(ApplicationDbContext db, AiDetectionCompletedEvent detection, CancellationToken ct)
    {
        // Idempotency check: check if anomaly already recorded
        var existingAnomaly = await db.DetectedAnomalies
            .FirstOrDefaultAsync(a => a.Id == detection.AnomalyId ||
                                     (!string.IsNullOrWhiteSpace(a.AiDetectionId) && a.AiDetectionId == detection.AnomalyId.ToString()), ct);

        if (existingAnomaly != null)
        {
            _logger.LogInformation("Detected anomaly {AnomalyId} already exists in OperationsService, skipping.", detection.AnomalyId);
            return;
        }

        var anomaly = new DetectedAnomaly
        {
            Id = detection.AnomalyId != Guid.Empty ? detection.AnomalyId : Guid.NewGuid(),
            MediaId = detection.MediaId,
            AssetId = detection.AssetId != Guid.Empty ? detection.AssetId : (Guid?)null,
            TowerId = detection.TowerId?.ToString(),
            AiDetectionId = detection.AnomalyId.ToString(),
            ImageUrl = detection.ImageUrl,
            CropUrl = detection.CropUrl,
            ConfidenceScore = detection.Confidence > 0 ? detection.Confidence : 0.9,
            ValidationStatus = "Pending",
            AiSource = "AIInspectionService",
            AnalystNotes = $"Detected defect: {detection.DefectType} ({detection.Severity})",
            CreatedAt = detection.DetectedAt != default ? detection.DetectedAt : DateTime.UtcNow
        };

        db.DetectedAnomalies.Add(anomaly);

        // If emergency class / critical, create EmergencyAlert
        if (detection.IsEmergencyClass || string.Equals(detection.Severity, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            var alert = new EmergencyAlert
            {
                Id = Guid.NewGuid(),
                AnomalyId = anomaly.Id,
                AssetId = anomaly.AssetId,
                TowerId = detection.TowerId,
                MissionId = detection.MissionId,
                Status = "Active",
                Priority = "Emergency",
                DeliveryLatencySeconds = 2,
                TriggeredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            db.EmergencyAlerts.Add(alert);
        }

        // Increment active defect count on tower if tower exists
        if (detection.TowerId.HasValue)
        {
            var tower = await db.Towers.FindAsync(new object[] { detection.TowerId.Value }, ct);
            if (tower != null)
            {
                tower.ActiveDefectsCount++;
                tower.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Recorded defect anomaly {AnomalyId} in OperationsService.", anomaly.Id);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
