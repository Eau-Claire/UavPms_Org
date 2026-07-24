using System.Threading.Tasks;

namespace UavPms.IdentityService.Domain.Interfaces.Services;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event) where T : class;
}
