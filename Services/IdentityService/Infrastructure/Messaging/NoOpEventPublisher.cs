using System.Threading.Tasks;
using UavPms.IdentityService.Domain.Interfaces.Services;

namespace UavPms.IdentityService.Infrastructure.Messaging;

public class NoOpEventPublisher : IEventPublisher
{
    public Task PublishAsync<T>(T @event) where T : class
    {
        return Task.CompletedTask;
    }
}
