using System.Threading;
using System.Threading.Tasks;
using UavPms.Shared.Contracts.Events;

namespace UavPms.OperationsService.Application.Common.Interfaces;

public interface IMissionRealtimeNotifier
{
    Task NotifyAsync(MissionLifecycleEventDto evt, CancellationToken cancellationToken = default);
}
