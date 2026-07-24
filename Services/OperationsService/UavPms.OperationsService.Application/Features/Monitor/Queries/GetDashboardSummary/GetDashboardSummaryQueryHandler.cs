using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Monitor.Queries.GetDashboardSummary;

public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IMonitorRepository _monitorRepository;

    public GetDashboardSummaryQueryHandler(IMonitorRepository monitorRepository)
    {
        _monitorRepository = monitorRepository;
    }

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var model = await _monitorRepository.GetSummaryAsync(cancellationToken);

        return new DashboardSummaryDto(
            model.TotalMissions,
            model.PendingMissions,
            model.InProgressMissions,
            model.CompletedMissions,
            model.TotalInspections,
            model.TotalDefects,
            model.CriticalDefects
        );
    }
}
