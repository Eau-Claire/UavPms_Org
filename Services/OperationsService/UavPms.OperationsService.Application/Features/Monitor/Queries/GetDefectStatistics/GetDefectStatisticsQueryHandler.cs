using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Monitor.Queries.GetDefectStatistics;

public class GetDefectStatisticsQueryHandler : IRequestHandler<GetDefectStatisticsQuery, DefectStatisticsDto>
{
    private readonly IMonitorRepository _monitorRepository;

    public GetDefectStatisticsQueryHandler(IMonitorRepository monitorRepository)
    {
        _monitorRepository = monitorRepository;
    }

    public async Task<DefectStatisticsDto> Handle(GetDefectStatisticsQuery request, CancellationToken cancellationToken)
    {
        var model = await _monitorRepository.GetDefectStatisticsAsync(cancellationToken);
        var stats = model.ByType.Select(x => new DefectTypeStatDto(x.DefectType, x.Count)).ToList();

        return new DefectStatisticsDto(model.TotalDefects, stats);
    }
}
