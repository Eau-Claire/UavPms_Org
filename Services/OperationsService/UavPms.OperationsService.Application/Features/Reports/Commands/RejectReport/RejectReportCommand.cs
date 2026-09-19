using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Reports.DTOs;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.RejectReport;

public record RejectReportCommand(Guid Id, string Reason) : IRequest<ReportDetailDto>;
