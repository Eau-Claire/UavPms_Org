using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UavPms.OperationsService.Application.Features.Monitor.Queries.GetDashboardSummary;

public record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public record DashboardSummaryDto(
    int TotalMissions,
    int PendingMissions,
    int InProgressMissions,
    int CompletedMissions,
    int TotalInspections,
    int TotalDefects,
    int CriticalDefects
);
