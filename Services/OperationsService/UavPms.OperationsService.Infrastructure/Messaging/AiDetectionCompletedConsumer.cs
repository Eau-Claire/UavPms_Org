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
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private readonly ILogger<AiDetectionCompletedConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;

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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await _rabbitMqConnection.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.ExchangeDeclareAsync(
                    exchange: EventTopologyConstants.Exchange,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: EventTopologyConstants.Queues.OperationsDefectRecord,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.QueueBindAsync(
                    queue: EventTopologyConstants.Queues.OperationsDefectRecord,
                    exchange: EventTopologyConstants.Exchange,
                    routingKey: EventTopologyConstants.RoutingKeys.AiDetectionCompleted,
                    cancellationToken: stoppingToken);

                await channel.QueueBindAsync(
                    queue: EventTopologyConstants.Queues.OperationsDefectRecord,
                    exchange: EventTopologyConstants.Exchange,
                    routingKey: EventTopologyConstants.RoutingKeys.LegacyDefectDetected,
                    cancellationToken: stoppingToken);

                var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };
                channel.ChannelShutdownAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };

                var consumer = new AsyncEventingBasicConsumer(channel);
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

                        await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing AI detection in OperationsService");
                        await channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: EventTopologyConstants.Queues.OperationsDefectRecord,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("AiDetectionCompletedConsumer listening on queue '{QueueName}'",
                    EventTopologyConstants.Queues.OperationsDefectRecord);

                await disconnected.Task.WaitAsync(stoppingToken);
                _logger.LogWarning("AiDetectionCompletedConsumer RabbitMQ connection lost; reconnecting.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("AiDetectionCompletedConsumer is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AiDetectionCompletedConsumer disconnected; retrying in {DelaySeconds}s.", ReconnectDelay.TotalSeconds);
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

    public async Task ProcessDefectRecordAsync(ApplicationDbContext db, AiDetectionCompletedEvent detection, CancellationToken ct)
    {
        // Idempotency check: check if anomaly already recorded (e.g. written directly by AIInspectionService in the shared DB)
        var existingAnomaly = await db.DetectedAnomalies
            .FirstOrDefaultAsync(a => a.Id == detection.AnomalyId ||
                                     (!string.IsNullOrWhiteSpace(a.AiDetectionId) && a.AiDetectionId == detection.AnomalyId.ToString()), ct);

        Guid targetAnomalyId;
        var hasChanges = false;

        if (existingAnomaly != null)
        {
            targetAnomalyId = existingAnomaly.Id;
            _logger.LogInformation("Detected anomaly {AnomalyId} already exists in shared database; checking EmergencyAlert state.", targetAnomalyId);
        }
        else
        {
            // AIInspectionService đã ghi run + anomaly vào CSDL dùng chung; nhánh này chỉ bù khi bản ghi chưa có.
            var runExists = detection.RequestId != Guid.Empty
                && await db.AIAnalysisRuns.AnyAsync(r => r.Id == detection.RequestId && r.MediaId == detection.MediaId, ct);
            var categoryId = await db.DefectCategories
                .Where(c => c.CategoryCode == detection.CategoryCode)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct)
                ?? await db.DefectCategories.Select(c => (int?)c.Id).FirstOrDefaultAsync(ct)
                ?? 1;

            var anomaly = new DetectedAnomaly
            {
                Id = detection.AnomalyId != Guid.Empty ? detection.AnomalyId : Guid.NewGuid(),
                MediaId = detection.MediaId,
                AnalysisRunId = runExists ? detection.RequestId : null,
                TowerId = detection.TowerId,
                CategoryId = categoryId,
                AiDetectionId = detection.AnomalyId.ToString(),
                ImageUrl = detection.ImageUrl,
                CropUrl = detection.CropUrl,
                ConfidenceScore = detection.Confidence > 0 ? detection.Confidence : 0.9,
                ValidationStatus = "Pending",
                AnalystNotes = $"Detected defect: {detection.DefectType} ({detection.Severity})",
                CreatedAt = detection.DetectedAt != default ? detection.DetectedAt : DateTime.UtcNow
            };

            db.DetectedAnomalies.Add(anomaly);
            targetAnomalyId = anomaly.Id;
            hasChanges = true;
        }

        // If emergency class / critical, ensure an active EmergencyAlert exists for targetAnomalyId
        if (detection.IsEmergencyClass || string.Equals(detection.Severity, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            var activeAlertExists = await db.EmergencyAlerts
                .AnyAsync(a => a.AnomalyId == targetAnomalyId && a.Status == "Active" && !a.IsDeleted, ct);

            if (!activeAlertExists)
            {
                var now = DateTime.UtcNow;
                var alert = new EmergencyAlert
                {
                    Id = Guid.NewGuid(),
                    AnomalyId = targetAnomalyId,
                    Status = "Active",
                    Priority = "Critical",
                    TriggeredAt = detection.DetectedAt != default ? detection.DetectedAt : now,
                    ReceivedAt = now,
                    Notes = $"Auto-generated from AI detection: {detection.DefectType} (confidence: {detection.Confidence:P1}, severity: {detection.Severity})",
                    CreatedAt = now
                };
                db.EmergencyAlerts.Add(alert);
                hasChanges = true;
                _logger.LogInformation("Created active EmergencyAlert {AlertId} for anomaly {AnomalyId}.", alert.Id, targetAnomalyId);
            }
        }

        if (hasChanges)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Processed AI defect record for anomaly {AnomalyId} in OperationsService.", targetAnomalyId);
        }
    }
}
