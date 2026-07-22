using System;
using System.Collections.Generic;
using MediatR;

namespace UavPms.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;

public record GetMissionAiDetectionsQuery(Guid MissionId) : IRequest<IReadOnlyList<MissionAiDetectionMediaDto>>;
