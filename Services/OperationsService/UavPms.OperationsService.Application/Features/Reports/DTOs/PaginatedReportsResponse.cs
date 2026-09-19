using System.Collections.Generic;
using UavPms.OperationsService.Application.Common.DTOs;

namespace UavPms.OperationsService.Application.Features.Reports.DTOs;

public record PaginatedReportsResponse(
    List<ReportDto> Items,
    PaginationMetaData Pagination
);
