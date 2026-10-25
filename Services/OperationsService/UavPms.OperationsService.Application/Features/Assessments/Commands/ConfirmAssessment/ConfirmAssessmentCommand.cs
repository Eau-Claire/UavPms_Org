using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;

namespace UavPms.OperationsService.Application.Features.Assessments.Commands.ConfirmAssessment;

public record ConfirmAssessmentCommand(Guid AssessmentId) : IRequest<PreMissionAssessmentDto>;
