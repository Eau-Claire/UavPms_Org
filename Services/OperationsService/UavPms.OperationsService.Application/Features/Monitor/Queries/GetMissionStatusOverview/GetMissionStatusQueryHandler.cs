using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Monitor.Queries.GetMissionStatusOverview;

public class GetMissionStatusQueryHandler : IRequestHandler<GetMissionStatusQuery, MissionStatusOverviewDto>
{
    private readonly IMonitorRepository _monitorRepository;

    public GetMissionStatusQueryHandler(IMonitorRepository monitorRepository)
    {
        _monitorRepository = monitorRepository;
    }

    public async Task<MissionStatusOverviewDto> Handle(GetMissionStatusQuery request, CancellationToken cancellationToken)
    {
        var model = await _monitorRepository.GetMissionStatusOverviewAsync(cancellationToken);

        return new MissionStatusOverviewDto(
            model.Pending,
            model.InProgress,
            model.Completed);
    }
}
