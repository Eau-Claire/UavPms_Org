using System;
using System.Collections.Generic;
using MediatR;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;

namespace UavPms.OperationsService.Application.Features.Assessments.Commands.CreatePreMissionAssessment;

public record CreatePreMissionAssessmentCommand(
    Guid RegionId,
    DateTime PlannedStart,
    DateTime PlannedEnd,
    IReadOnlyCollection<Guid> TowerIds,
    string? BoundaryWkt = null,
    string? IdempotencyKey = null,
    int? RequiredInspectors = null,
    int? RequiredAnalysts = null,
    int? RequiredTechnicians = null,
    int? MaxDronesLimit = null
) : IRequest<PreMissionAssessmentDto>;
