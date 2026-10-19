using System;
using System.Collections.Generic;
using MediatR;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;

namespace UavPms.OperationsService.Application.Features.Assessments.Commands.CreatePreMissionAssessment;

public record CreatePreMissionAssessmentCommand(
    Guid RegionId,
    DateTime PlannedStart,
    DateTime PlannedEnd,
    IReadOnlyCollection<Guid> AssetIds,
    string? BoundaryWkt = null,
    string? IdempotencyKey = null
) : IRequest<PreMissionAssessmentDto>;
