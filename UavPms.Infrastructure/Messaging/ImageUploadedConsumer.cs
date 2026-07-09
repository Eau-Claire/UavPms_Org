using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
using UavPms.Application.Features.Notifications.Commands.CreateNotification;
using UavPms.Core.Contracts;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Infrastructure.Messaging;

/// <summary>
/// Background consumer that listens for ImageUploaded events from RabbitMQ,
/// simulates AI defect analysis on the uploaded image, updates the InspectionMedia record,
/// creates a DetectedAnomaly record if a defect is found, and publishes a DefectDetectedEvent.
/// </summary>
public class ImageUploadedConsumer : BackgroundService
{
    private readonly ILogger<ImageUploadedConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _visionAiBaseUrl;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string ExchangeName = "identity-exchange";
    private const string QueueName = "inspection.image-uploaded";
    private const string RoutingKey = "identity.event.imageuploadedevent";

    public ImageUploadedConsumer(
        ILogger<ImageUploadedConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;
        _visionAiBaseUrl = configuration["VisionAI:BaseUrl"] ?? "http://localhost:8000";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImageUploadedConsumer is starting...");

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

            await _channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await _channel.QueueBindAsync(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: RoutingKey,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    var imageEvent = JsonSerializer.Deserialize<ImageUploadedEvent>(json);

                    if (imageEvent != null)
                    {
                        _logger.LogInformation(
                            "Received ImageUploadedEvent: MediaId={MediaId}, MissionId={MissionId}, FileUrl={FileUrl}",
                            imageEvent.MediaId, imageEvent.MissionId, imageEvent.FileUrl);

                        await ProcessImageAnalysisAsync(imageEvent);
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing ImageUploadedEvent");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("ImageUploadedConsumer is now listening on queue '{QueueName}'", QueueName);

            // Keep alive until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ImageUploadedConsumer is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImageUploadedConsumer encountered an error. It will not retry.");
        }
    }

    /// <summary>
    /// Gọi Vision AI service để phân tích ảnh, cập nhật kết quả vào DB.
    /// </summary>
    private async Task ProcessImageAnalysisAsync(ImageUploadedEvent imageEvent)
    {
        using var scope = _scopeFactory.CreateScope();
        var mediaRepository = scope.ServiceProvider.GetRequiredService<IGenericRepository<InspectionMedia>>();
        var anomalyRepository = scope.ServiceProvider.GetRequiredService<IGenericRepository<DetectedAnomaly>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        // Fetch the InspectionMedia record
        var media = await mediaRepository.GetByIdAsync(imageEvent.MediaId, track: true);
        if (media == null)
        {
            _logger.LogWarning("InspectionMedia with ID {MediaId} not found. Skipping analysis.", imageEvent.MediaId);
            return;
        }

        string responseBody;
        try
        {
            responseBody = await CallVisionAIServiceAsync(imageEvent.FileUrl, imageEvent.MediaType, "General");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Vision AI service for MediaId={MediaId}", imageEvent.MediaId);
            media.ValidationStatus = "Failed";
            media.UpdatedAt = DateTime.UtcNow;
            await mediaRepository.UpdateAsync(media);
            await unitOfWork.SaveChangesAsync();
            return;
        }

        // Parse Vision AI response
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        
        var hasAnomaly = false;
        if (root.TryGetProperty("summary", out var summaryElement) &&
            summaryElement.TryGetProperty("hasAnomaly", out var hasAnomalyProp))
        {
            hasAnomaly = hasAnomalyProp.GetBoolean();
        }

        var detectedAt = DateTime.UtcNow;
        media.AiSource = "Vision-Base-Human-Motion-Detection-AI";
        media.ValidationStatus = hasAnomaly ? "DefectDetected" : "NoDefect";
        media.UpdatedAt = detectedAt;
        await mediaRepository.UpdateAsync(media);

        _logger.LogInformation(
            "AI analysis completed for MediaId={MediaId}: IsDefect={IsDefect}",
            imageEvent.MediaId, hasAnomaly);

        if (hasAnomaly)
        {
            var confidenceScore = 0.8;
            var defectType = "Anomaly";
            string boundingBoxJson = string.Empty;

            if (root.TryGetProperty("detections", out var detectionsProp) && detectionsProp.ValueKind == JsonValueKind.Array)
            {
                var anomalyDetection = detectionsProp.EnumerateArray()
                    .FirstOrDefault(d => d.TryGetProperty("eventType", out var eventTypeProp) && eventTypeProp.GetString() == "Anomaly");

                if (anomalyDetection.ValueKind != JsonValueKind.Undefined)
                {
                    if (anomalyDetection.TryGetProperty("confidence", out var confProp))
                    {
                        confidenceScore = confProp.GetDouble();
                    }
                    if (anomalyDetection.TryGetProperty("className", out var classProp))
                    {
                        defectType = classProp.GetString() ?? "Anomaly";
                    }
                    if (anomalyDetection.TryGetProperty("boundingBox", out var bboxProp))
                    {
                        if (bboxProp.TryGetProperty("x1", out var x1) &&
                            bboxProp.TryGetProperty("y1", out var y1) &&
                            bboxProp.TryGetProperty("x2", out var x2) &&
                            bboxProp.TryGetProperty("y2", out var y2))
                        {
                            var x1Val = x1.GetDouble();
                            var y1Val = y1.GetDouble();
                            var x2Val = x2.GetDouble();
                            var y2Val = y2.GetDouble();
                            
                            boundingBoxJson = JsonSerializer.Serialize(new
                            {
                                x = x1Val,
                                y = y1Val,
                                width = x2Val - x1Val,
                                height = y2Val - y1Val
                            });
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(boundingBoxJson))
            {
                boundingBoxJson = JsonSerializer.Serialize(new
                {
                    x = 100,
                    y = 100,
                    width = 200,
                    height = 200
                });
            }

            // Create DetectedAnomaly record
            var anomaly = new DetectedAnomaly
            {
                Id = Guid.NewGuid(),
                MediaId = imageEvent.MediaId,
                AssetId = media.AssetId,
                CategoryId = 1, // Default category, will be refined by human analyst
                ConfidenceScore = Math.Round(confidenceScore, 2),
                ValidationStatus = "Pending",
                AiSource = "Vision-Base-Human-Motion-Detection-AI",
                AnalystNotes = $"Auto-detected by AI: {defectType}",
                BoundingBox = boundingBoxJson,
                CreatedAt = detectedAt
            };

            await anomalyRepository.AddAsync(anomaly);
            await unitOfWork.SaveChangesAsync();

            _logger.LogWarning(
                "Defect detected! AnomalyId={AnomalyId}, MediaId={MediaId}, Type={DefectType}, Confidence={Confidence}",
                anomaly.Id, imageEvent.MediaId, defectType, confidenceScore);

            // Publish DefectDetectedEvent
            try
            {
                await eventPublisher.PublishAsync(new DefectDetectedEvent
                {
                    InspectionId = imageEvent.MediaId,
                    RecordId = anomaly.Id,
                    MissionId = imageEvent.MissionId,
                    ImageUrl = imageEvent.FileUrl,
                    IsDefect = true,
                    DefectType = defectType,
                    DetectedAt = detectedAt
                });

                _logger.LogInformation(
                    "Published DefectDetectedEvent for AnomalyId={AnomalyId}, MissionId={MissionId}",
                    anomaly.Id, imageEvent.MissionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish DefectDetectedEvent for AnomalyId={AnomalyId}. The anomaly was saved successfully.",
                    anomaly.Id);
            }

            // Directly notify admins/managers (in case DefectDetectedConsumer is not running)
            try
            {
                var admins = await userRepository.GetUsersByRoleAsync("SystemAdmin");
                var managers = await userRepository.GetUsersByRoleAsync("Manager");

                var usersToNotify = admins.Concat(managers)
                    .DistinctBy(u => u.Id)
                    .ToList();

                foreach (var user in usersToNotify)
                {
                    await mediator.Send(new CreateNotificationCommand(
                        user.Id,
                        "CriticalAlert",
                        "DetectedAnomaly",
                        anomaly.Id,
                        "⚠️ AI phát hiện khuyết tật từ ảnh kiểm tra",
                        $"AI đã phát hiện khuyết tật '{defectType}' (độ tin cậy: {confidenceScore:P0}) trong ảnh kiểm tra. Cần xem xét và xác nhận."
                    ));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send direct notifications for AnomalyId={AnomalyId}", anomaly.Id);
            }
        }
        else
        {
            await unitOfWork.SaveChangesAsync();
            _logger.LogInformation("No defect detected for MediaId={MediaId}. Record updated.", imageEvent.MediaId);
        }
    }

    /// <summary>
    /// Gọi endpoint /api/analyze của Vision AI service.
    /// </summary>
    private async Task<string> CallVisionAIServiceAsync(string fileUrl, string mediaType, string analysisType)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        var fileName = Path.GetFileName(fileUrl);
        var storagePath = Path.Combine(Directory.GetCurrentDirectory(), "uav_storage", "images", fileName);

        if (!File.Exists(storagePath))
        {
            throw new FileNotFoundException($"File not found at: {storagePath}");
        }

        using var form = new MultipartFormDataContent();

        var fileBytes = await File.ReadAllBytesAsync(storagePath);
        var fileContent = new ByteArrayContent(fileBytes);
        
        var contentType = mediaType == "Video" ? "video/mp4" : "image/jpeg";
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        form.Add(new StringContent(analysisType), "analysis_type");

        var endpoint = $"{_visionAiBaseUrl}/api/analyze";

        _logger.LogInformation("Calling Vision AI service: {Endpoint}, File: {FileName}, MediaType: {MediaType}", endpoint, fileName, mediaType);

        var response = await httpClient.PostAsync(endpoint, form);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Vision AI service returned {response.StatusCode}: {responseBody}");
        }

        _logger.LogInformation("Vision AI response received for file {FileName}", fileName);

        return responseBody;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
