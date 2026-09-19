using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Reports.DTOs;

namespace UavPms.OperationsService.Application.Features.Reports.Queries.GetReportsPaged;

public record GetReportsPagedQuery(
    int Page = 1,
    int PageSize = 10,
    string? Type = null,
    string? Status = null,
    Guid? TransmissionLineId = null,
    Guid? SubstationId = null,
    string? Search = null,
    string? SortBy = null,
    string? SortOrder = null
) : IRequest<PaginatedReportsResponse>;
