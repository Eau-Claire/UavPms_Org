using System;
using MediatR;

namespace UavPms.OperationsService.Application.Features.Assessments.Queries.EvaluateResourceReadiness;

public record EvaluateResourceReadinessQuery(Guid AssessmentId) : IRequest<ResourceReadinessDto>;
