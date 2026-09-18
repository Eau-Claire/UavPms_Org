using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Reports.DTOs;

namespace UavPms.OperationsService.Application.Features.Reports.Queries.GetReportById;

public record GetReportByIdQuery(Guid Id) : IRequest<ReportDetailDto>;
