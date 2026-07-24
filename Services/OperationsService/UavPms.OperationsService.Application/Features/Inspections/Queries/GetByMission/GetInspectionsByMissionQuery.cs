using System;
using System.Collections.Generic;
using MediatR;
using UavPms.OperationsService.Application.Features.Inspections.DTOs;

namespace UavPms.OperationsService.Application.Features.Inspections.Queries.GetByMission;

public record GetInspectionsByMissionQuery(Guid MissionId) : IRequest<IReadOnlyList<InspectionReportDto>>;
