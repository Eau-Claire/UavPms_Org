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
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Infrastructure.Messaging;

/// <summary>
/// Background consumer lắng nghe AIAnalysisRequestedEvent từ RabbitMQ.
/// Gọi Vision AI service để phân tích ảnh, cập nhật kết quả vào DB.
/// </summary>
public class AIAnalysisRequestedConsumer : BackgroundService
{
    private readonly ILogger<AIAnalysisRequestedConsumer> _logger;
    private readonly RabbitMqConnection _rabbitMqConnection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _visionAiBaseUrl;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string ExchangeName = "identity-exchange";
    private const string QueueName = "ai-analysis.requested";
    private const string RoutingKey = "identity.event.aianalysisrequestedevent";
    private const string FailedQueueName = "ai-analysis.failed";
    private const string FailedRoutingKey = "identity.event.aianalysisfailed";

    public AIAnalysisRequestedConsumer(
        ILogger<AIAnalysisRequestedConsumer> logger,
        RabbitMqConnection rabbitMqConnection,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _scopeFactory = scopeFactory;

        // URL Vision AI service (mặc định http://localhost:8000)
        _visionAiBaseUrl = configuration["VisionAI:BaseUrl"] ?? "http://localhost:8000";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AIAnalysisRequestedConsumer is starting...");

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

            // Declare DLQ queue and bind
            await _channel.QueueDeclareAsync(
                queue: FailedQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await _channel.QueueBindAsync(
                queue: FailedQueueName,
                exchange: ExchangeName,
                routingKey: FailedRoutingKey,
                cancellationToken: stoppingToken);

            // Declare main queue and bind
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
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var analysisEvent = JsonSerializer.Deserialize<AIAnalysisRequestedEvent>(json);

                if (analysisEvent == null)
                {
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                // Get retry count from headers
                long retryCount = 0;
                if (ea.BasicProperties.Headers != null && ea.BasicProperties.Headers.TryGetValue("x-retry-count", out var countObj))
                {
                    if (countObj is int countInt) retryCount = countInt;
                    else if (countObj is long countLong) retryCount = countLong;
                    else if (countObj is string countStr && long.TryParse(countStr, out var parsedLong)) retryCount = parsedLong;
                    else if (countObj is byte[] bytes)
                    {
                        try { retryCount = BitConverter.ToInt64(bytes, 0); } catch { }
                    }
                }

                _logger.LogInformation(
                    "Received AIAnalysisRequestedEvent: RequestId={RequestId}, AnalysisType={AnalysisType}, Retry={RetryCount}",
                    analysisEvent.RequestId, analysisEvent.AnalysisType, retryCount);

                try
                {
                    await ProcessAIAnalysisAsync(analysisEvent);
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing AIAnalysisRequestedEvent for RequestId={RequestId}", analysisEvent.RequestId);

                    if (retryCount < 2) // Total 3 attempts (retryCount = 0, 1, 2)
                    {
                        var nextRetry = retryCount + 1;
                        _logger.LogWarning("AI analysis call failed. Retrying ({Count}/3) in 2 seconds...", nextRetry);
                        
                        await Task.Delay(2000, stoppingToken);

                        var props = new BasicProperties
                        {
                            Persistent = true,
                            Headers = new Dictionary<string, object?>
                            {
                                { "x-retry-count", nextRetry }
                            }
                        };

                        await _channel.BasicPublishAsync(
                            exchange: ExchangeName,
                            routingKey: RoutingKey,
                            mandatory: true,
                            basicProperties: props,
                            body: body,
                            cancellationToken: stoppingToken);

                        await _channel.BasicAckAsync(ea.DeliveryTag, false);
                    }
                    else
                    {
                        _logger.LogError("AI analysis failed after 3 attempts. Marking as Failed in DB and forwarding to DLQ.");

                        // 1. Mark status = Failed in DB
                        await MarkRequestAsFailedAsync(analysisEvent.RequestId, ex.Message);

                        // 2. Publish to DLQ queue
                        var props = new BasicProperties { Persistent = true };
                        await _channel.BasicPublishAsync(
                            exchange: ExchangeName,
                            routingKey: FailedRoutingKey,
                            mandatory: true,
                            basicProperties: props,
                            body: body,
                            cancellationToken: stoppingToken);

                        await _channel.BasicAckAsync(ea.DeliveryTag, false);
                    }
                }
            };

            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("AIAnalysisRequestedConsumer is now listening on queue '{QueueName}'", QueueName);

            // Keep alive until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("AIAnalysisRequestedConsumer is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AIAnalysisRequestedConsumer encountered an error. It will not retry.");
        }
    }

    /// <summary>
    /// Gọi Vision AI service để phân tích ảnh, cập nhật kết quả vào DB.
    /// Ném ngoại lệ ra ngoài để kích hoạt vòng lặp retry.
    /// </summary>
    private async Task ProcessAIAnalysisAsync(AIAnalysisRequestedEvent analysisEvent)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<AIAnalysisRequest>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        // Lấy record từ DB
        var request = await repository.GetByIdAsync(analysisEvent.RequestId, track: true);
        if (request == null)
        {
            _logger.LogWarning("AIAnalysisRequest with ID {RequestId} not found. Skipping.", analysisEvent.RequestId);
            return;
        }

        // Cập nhật trạng thái → Processing
        request.Status = AIAnalysisStatus.Processing;
        request.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(request);
        await unitOfWork.SaveChangesAsync();

        // Gọi Vision AI service (có thể ném ngoại lệ nếu AI down)
        var aiResult = await CallVisionAIServiceAsync(analysisEvent.FileUrl, analysisEvent.MediaType, analysisEvent.AnalysisType);

        // Cập nhật kết quả thành công
        request.Status = AIAnalysisStatus.Completed;
        request.Result = aiResult;
        request.CompletedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(request);
        await unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "AI analysis completed for RequestId={RequestId}. Result stored.",
            analysisEvent.RequestId);

        // Thông báo thành công cho người upload
        try
        {
            await mediator.Send(new CreateNotificationCommand(
                request.UploadedBy,
                "Information",
                "AIAnalysisRequest",
                request.Id,
                "🔬 Kết quả phân tích AI đã hoàn thành",
                $"Yêu cầu phân tích AI (ID: {request.Id.ToString()[..8]}...) đã hoàn thành. Vui lòng kiểm tra kết quả."
            ));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification for RequestId={RequestId}", request.Id);
        }
    }

    /// <summary>
    /// Đánh dấu yêu cầu phân tích thất bại trong database khi hết số lần retry.
    /// </summary>
    private async Task MarkRequestAsFailedAsync(Guid requestId, string errorMessage)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<AIAnalysisRequest>>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

            var request = await repository.GetByIdAsync(requestId, track: true);
            if (request != null)
            {
                request.Status = AIAnalysisStatus.Failed;
                request.Result = JsonSerializer.Serialize(new { error = errorMessage });
                request.CompletedAt = DateTime.UtcNow;
                request.UpdatedAt = DateTime.UtcNow;

                await repository.UpdateAsync(request);
                await unitOfWork.SaveChangesAsync();

                try
                {
                    await mediator.Send(new CreateNotificationCommand(
                        request.UploadedBy,
                        "Information",
                        "AIAnalysisRequest",
                        request.Id,
                        "🔬 Kết quả phân tích AI đã thất bại",
                        $"Yêu cầu phân tích AI (ID: {request.Id.ToString()[..8]}...) đã thất bại sau 3 lần thử. Vui lòng kiểm tra lại."
                    ));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send failure notification for RequestId={RequestId}", request.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking request {RequestId} as failed in database", requestId);
        }
    }

    /// <summary>
    /// Gọi endpoint /api/analyze của Vision AI service.
    /// Tải file từ local storage và gửi tới AI service.
    /// </summary>
    private async Task<string> CallVisionAIServiceAsync(string fileUrl, string mediaType, string analysisType)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        // fileUrl có dạng /images/xxx.jpg hoặc /images/xxx.mp4 — cần đọc file từ local storage
        var fileName = Path.GetFileName(fileUrl);
        var storagePath = Path.Combine(Directory.GetCurrentDirectory(), "uav_storage", "images", fileName);

        if (!File.Exists(storagePath))
        {
            throw new FileNotFoundException($"File not found at: {storagePath}");
        }

        using var form = new MultipartFormDataContent();

        // Đọc file và gửi tới Vision AI
        var fileBytes = await File.ReadAllBytesAsync(storagePath);
        var fileContent = new ByteArrayContent(fileBytes);
        
        var contentType = mediaType == "Video" ? "video/mp4" : "image/jpeg";
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        // Gửi analysis type
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
