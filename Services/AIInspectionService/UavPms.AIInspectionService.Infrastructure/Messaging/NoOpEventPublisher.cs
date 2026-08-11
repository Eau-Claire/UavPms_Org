using System.Threading.Tasks;
using UavPms.AIInspectionService.Domain.Interfaces.Services;

namespace UavPms.AIInspectionService.Infrastructure.Messaging;

public class NoOpEventPublisher : IEventPublisher
{
    public Task PublishAsync<T>(T @event) where T : class
    {
        return Task.CompletedTask;
    }
}
