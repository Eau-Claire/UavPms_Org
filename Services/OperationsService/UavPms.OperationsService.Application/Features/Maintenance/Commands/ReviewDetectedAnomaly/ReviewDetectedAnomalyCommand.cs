using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.Maintenance.Commands.ReviewDetectedAnomaly;

public record ReviewDetectedAnomalyCommand(
    Guid MissionId,
    Guid AnomalyId,
    string Status,
    string? ReviewNotes = null,
    string? AnalystFeedbackJson = null,
    string? OverrideSeverity = null
) : IRequest<ReviewDetectionResultDto>;
