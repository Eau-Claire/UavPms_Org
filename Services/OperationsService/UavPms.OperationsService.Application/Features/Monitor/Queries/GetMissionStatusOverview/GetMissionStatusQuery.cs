using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UavPms.OperationsService.Application.Features.Monitor.Queries.GetMissionStatusOverview;

public record GetMissionStatusQuery : IRequest<MissionStatusOverviewDto>;

public record MissionStatusOverviewDto(
    int Pending,
    int InProgress,
    int Completed
);
