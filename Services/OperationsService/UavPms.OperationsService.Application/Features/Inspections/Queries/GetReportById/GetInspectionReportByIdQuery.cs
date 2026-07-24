using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Inspections.DTOs;

namespace UavPms.OperationsService.Application.Features.Inspections.Queries.GetReportById;

public record GetInspectionReportByIdQuery(Guid Id) : IRequest<InspectionReportDto>;
