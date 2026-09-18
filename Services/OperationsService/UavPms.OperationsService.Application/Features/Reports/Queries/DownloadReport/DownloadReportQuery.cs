using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Reports.DTOs;

namespace UavPms.OperationsService.Application.Features.Reports.Queries.DownloadReport;

public record DownloadReportQuery(Guid Id, string Format = "pdf") : IRequest<ReportFileDownloadDto>;
