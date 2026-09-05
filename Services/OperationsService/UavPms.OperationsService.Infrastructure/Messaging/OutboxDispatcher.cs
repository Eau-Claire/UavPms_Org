using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Events;

namespace UavPms.OperationsService.Infrastructure.Messaging;

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
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
                var messages = await db.OutboxMessages.Where(x => x.PublishedAt == null && !x.IsDeleted &&
                        x.MessageType == nameof(InspectionMediaUploadedEvent))
                    .OrderBy(x => x.OccurredAt).Take(20).ToListAsync(stoppingToken);
                foreach (var message in messages)
                {
                    try
                    {
                        var payload = JsonSerializer.Deserialize<InspectionMediaUploadedEvent>(message.Payload)
                            ?? throw new JsonException("Invalid inspection media outbox payload.");
                        await publisher.PublishAsync(payload);
                        message.PublishedAt = DateTime.UtcNow;
                        message.UpdatedAt = DateTime.UtcNow;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        message.Attempts++;
                        message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                        _logger.LogWarning(ex, "Failed to publish outbox message {MessageId}", message.Id);
                    }
                }

                if (messages.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatch iteration failed; retrying after a bounded delay.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
