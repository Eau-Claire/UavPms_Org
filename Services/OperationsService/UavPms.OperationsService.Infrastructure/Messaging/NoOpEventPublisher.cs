using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Infrastructure.Messaging;

public class NoOpEventPublisher : IEventPublisher
{
    public Task PublishAsync<T>(T @event) where T : class
    {
        return Task.CompletedTask;
    }
}
