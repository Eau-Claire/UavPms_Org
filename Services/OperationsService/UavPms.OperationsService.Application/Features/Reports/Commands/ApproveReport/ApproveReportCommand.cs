using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Reports.DTOs;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.ApproveReport;

public record ApproveReportCommand(Guid Id) : IRequest<ReportDetailDto>;
