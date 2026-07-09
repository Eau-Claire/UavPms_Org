using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UavPms.Application.Features.Monitor.DTOs;

public record DashboardSummaryDto(
    int TotalMissions,
    int PendingMissions,
    int InProgressMissions,
    int CompletedMissions,
    int TotalInspections,
    int TotalDefects,
    int CriticalDefects
);