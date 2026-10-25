using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.CreateMissionFromAssessment;

public record CreateMissionFromAssessmentCommand(
    CreateMissionFromAssessmentRequest Request
) : IRequest<MissionOperationResultDto>;
