using System;
using System.Collections.Generic;
using MediatR;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;

public record GetMissionAiDetectionsQuery(Guid MissionId) : IRequest<IReadOnlyList<MissionAiDetectionMediaDto>>;
