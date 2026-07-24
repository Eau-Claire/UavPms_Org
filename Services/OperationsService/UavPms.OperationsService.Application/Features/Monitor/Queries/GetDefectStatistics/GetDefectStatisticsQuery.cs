using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;

namespace UavPms.OperationsService.Application.Features.Monitor.Queries.GetDefectStatistics;

public record GetDefectStatisticsQuery : IRequest<DefectStatisticsDto>;

public record DefectTypeStatDto(
    string Type,
    int Count
);

public record DefectStatisticsDto(
    int TotalDefects,
    List<DefectTypeStatDto> ByType
);
