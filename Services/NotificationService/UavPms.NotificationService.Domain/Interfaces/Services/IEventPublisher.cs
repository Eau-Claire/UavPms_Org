using System.Threading.Tasks;

namespace UavPms.NotificationService.Domain.Interfaces.Services;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event) where T : class;
}
