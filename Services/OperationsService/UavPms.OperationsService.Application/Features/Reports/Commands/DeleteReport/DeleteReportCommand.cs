using System;
using MediatR;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.DeleteReport;

public record DeleteReportCommand(Guid Id) : IRequest<bool>;
