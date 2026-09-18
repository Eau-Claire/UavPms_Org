using System.Collections.Generic;

namespace UavPms.OperationsService.Application.Features.Reports.DTOs;

public record ReportStatisticsDto(
    int Total,
    Dictionary<string, int> ByType,
    Dictionary<string, int> ByStatus
);
