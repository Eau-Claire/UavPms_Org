using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.AIInspectionService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Events;

namespace UavPms.AIInspectionService.Infrastructure.Messaging;

public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcher> _logger;
    public OutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcher> logger)
    { _scopeFactory = scopeFactory; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
            var messages = await db.OutboxMessages.Where(x => x.PublishedAt == null && !x.IsDeleted &&
                    (x.MessageType == nameof(AIAnalysisRequestedEvent) ||
                     x.MessageType == nameof(DefectDetectedEvent) ||
                     x.MessageType == nameof(AIAnalysisStatusChangedEvent)))
                .OrderBy(x => x.OccurredAt).Take(20).ToListAsync(stoppingToken);
            foreach (var message in messages)
            {
                try
                {
                    await PublishAsync(publisher, message.MessageType, message.Payload);
                    message.PublishedAt = DateTime.UtcNow;
                    message.UpdatedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    message.Attempts++;
                    message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                    _logger.LogWarning(ex, "Failed to publish outbox message {MessageId}", message.Id);
                }
            }
            if (messages.Count > 0) await db.SaveChangesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private static Task PublishAsync(IEventPublisher publisher, string messageType, string payload) => messageType switch
    {
        nameof(AIAnalysisRequestedEvent) => publisher.PublishAsync(
            JsonSerializer.Deserialize<AIAnalysisRequestedEvent>(payload) ?? throw new JsonException("Invalid AI request outbox payload.")),
        nameof(DefectDetectedEvent) => publisher.PublishAsync(
            JsonSerializer.Deserialize<DefectDetectedEvent>(payload) ?? throw new JsonException("Invalid defect outbox payload.")),
        nameof(AIAnalysisStatusChangedEvent) => publisher.PublishAsync(
            JsonSerializer.Deserialize<AIAnalysisStatusChangedEvent>(payload) ?? throw new JsonException("Invalid status outbox payload.")),
        _ => throw new InvalidOperationException($"Unsupported outbox message type {messageType}.")
    };
}
