using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Reports.DTOs;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.GenerateReport;

public record GenerateReportCommand(Guid Id, string Format = "pdf") : IRequest<ReportDetailDto>;
