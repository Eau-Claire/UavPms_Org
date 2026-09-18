using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Reports.DTOs;

namespace UavPms.OperationsService.Application.Features.Reports.Queries.GetReportStatistics;

public record GetReportStatisticsQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Period = null
) : IRequest<ReportStatisticsDto>;
