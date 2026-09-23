using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Events;

namespace UavPms.OperationsService.Infrastructure.Services;

public class MissionRealtimeNotifier : IMissionRealtimeNotifier
{
    private readonly IEventPublisher _eventPublisher;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MissionRealtimeNotifier> _logger;

    public MissionRealtimeNotifier(
        IEventPublisher eventPublisher,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MissionRealtimeNotifier> logger)
    {
        _eventPublisher = eventPublisher;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task NotifyAsync(MissionLifecycleEventDto evt, CancellationToken cancellationToken = default)
    {
        if (evt == null || string.IsNullOrWhiteSpace(evt.MissionId)) return;

        // 1. Publish to RabbitMQ Event Bus
        try
        {
            var realtimeEvent = new MissionLifecycleRealtimeEvent
            {
                Event = evt,
                CreatedAt = DateTime.UtcNow
            };
            await _eventPublisher.PublishAsync(realtimeEvent);
            _logger.LogInformation("Published MissionLifecycleRealtimeEvent to event bus for mission {MissionId}, type {Type}", evt.MissionId, evt.Type);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish MissionLifecycleRealtimeEvent to event bus. Attempting HTTP direct broadcast fallback.");
        }

        // 2. Direct HTTP broadcast fallback to NotificationService
        try
        {
            var baseUrl = _configuration["Services:NotificationServiceUrl"]
                ?? _configuration["NotificationService:BaseUrl"]
                ?? "http://notificationservice:8080";

            var endpoint = $"{baseUrl.TrimEnd('/')}/api/v1/notifications/realtime/mission-event";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(3);

            var json = JsonSerializer.Serialize(evt);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Successfully dispatched direct HTTP realtime event for mission {MissionId}", evt.MissionId);
            }
            else
            {
                _logger.LogDebug("NotificationService direct HTTP dispatch returned status {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Do not fail user action if notification service HTTP endpoint is unreachable
            _logger.LogDebug(ex, "Direct HTTP realtime dispatch skipped/failed for mission {MissionId}", evt.MissionId);
        }
    }
}
