using System;
using System.Collections.Generic;
using MediatR;
using UavPms.OperationsService.Application.Features.Reports.DTOs;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.UpdateReport;

public record UpdateReportCommand(
    Guid Id,
    string Title,
    string? Description,
    Guid? TransmissionLineId,
    Guid? SubstationId,
    List<Guid>? MissionIds,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo
) : IRequest<ReportDetailDto>;
