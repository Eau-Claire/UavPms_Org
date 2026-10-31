using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Monitor.Queries.GetActiveAlerts;

public class GetActiveAlertsQueryHandler : IRequestHandler<GetActiveAlertsQuery, ActiveAlertsResponse>
{
    private readonly IMonitorRepository _monitorRepository;
    private readonly ICurrentUserServices _currentUser;

    public GetActiveAlertsQueryHandler(IMonitorRepository monitorRepository, ICurrentUserServices currentUser)
    {
        _monitorRepository = monitorRepository;
        _currentUser = currentUser;
    }

    public async Task<ActiveAlertsResponse> Handle(GetActiveAlertsQuery request, CancellationToken cancellationToken)
    {
        var alerts = await _monitorRepository.GetActiveAlertsAsync(_currentUser.UserId, cancellationToken);
        var dtos = alerts.Select(a => new ActiveAlertDto(
            a.NotificationId,
            a.Title,
            a.Message,
            a.CreatedAt,
            a.IsRead
        )).ToList();

        return new ActiveAlertsResponse(dtos);
    }
}
