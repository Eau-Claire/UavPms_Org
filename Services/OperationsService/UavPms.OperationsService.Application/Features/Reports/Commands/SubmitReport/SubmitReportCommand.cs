using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Reports.DTOs;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.SubmitReport;

public record SubmitReportCommand(Guid Id) : IRequest<ReportDetailDto>;
