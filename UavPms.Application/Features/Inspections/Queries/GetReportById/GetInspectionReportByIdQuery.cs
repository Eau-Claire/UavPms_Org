using System;
using MediatR;
using UavPms.Application.Features.Inspections.DTOs;

namespace UavPms.Application.Features.Inspections.Queries.GetReportById;

public record GetInspectionReportByIdQuery(Guid Id) : IRequest<InspectionReportDto>;
