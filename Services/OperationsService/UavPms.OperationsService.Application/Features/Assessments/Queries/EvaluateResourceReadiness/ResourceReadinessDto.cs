using System;
using System.Collections.Generic;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;

namespace UavPms.OperationsService.Application.Features.Assessments.Queries.EvaluateResourceReadiness;

public record ResourceReadinessDto(
    Guid AssessmentId,
    string Status,
    string SiteFeasibilityStatus,
    string OverallTechnicalHealth,
    bool IsReady,
    IReadOnlyList<PreMissionAssessmentPersonnelDto> PersonnelCandidates,
    IReadOnlyList<PreMissionAssessmentDroneDto> DroneCandidates,
    DateTime EvaluatedAt
);
