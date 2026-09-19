using System;
using System.Collections.Generic;
using MediatR;
using UavPms.OperationsService.Application.Features.Reports.DTOs;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.CreateReport;

public record CreateReportCommand(
    string Title,
    string Type,
    Guid? TransmissionLineId,
    Guid? SubstationId,
    List<Guid>? MissionIds,
    string? Description,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo
) : IRequest<ReportDetailDto>;
