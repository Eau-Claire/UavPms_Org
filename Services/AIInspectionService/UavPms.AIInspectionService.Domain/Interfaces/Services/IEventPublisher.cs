using System.Threading.Tasks;

namespace UavPms.AIInspectionService.Domain.Interfaces.Services;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event) where T : class;
}
