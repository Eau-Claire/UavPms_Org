using MediatR;
using UavPms.Shared.Contracts.Events;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.StartUploadedMediaAnalysis;

public sealed record StartUploadedMediaAnalysisCommand(InspectionMediaUploadedEvent Upload)
    : IRequest<Guid>;
