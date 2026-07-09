using System;
using System.Collections.Generic;
using MediatR;
using UavPms.Application.Features.Inspections.DTOs;

namespace UavPms.Application.Features.Inspections.Queries.GetByMission;

public record GetInspectionsByMissionQuery(Guid MissionId) : IRequest<IReadOnlyList<InspectionReportDto>>;
