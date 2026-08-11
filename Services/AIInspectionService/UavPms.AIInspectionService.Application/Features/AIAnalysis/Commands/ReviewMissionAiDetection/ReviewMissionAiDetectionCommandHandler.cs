using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ReviewMissionAiDetection;

public class ReviewMissionAiDetectionCommandHandler
    : IRequestHandler<ReviewMissionAiDetectionCommand, MissionAiDetectionDto>
{
    private readonly IAnomalyRepository _anomalyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserServices _currentUser;

    public ReviewMissionAiDetectionCommandHandler(
        IAnomalyRepository anomalyRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserServices currentUser)
    {
        _anomalyRepository = anomalyRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<MissionAiDetectionDto> Handle(
        ReviewMissionAiDetectionCommand request,
        CancellationToken cancellationToken)
    {
        var anomaly = await _anomalyRepository.GetByIdWithDetailAsync(request.DetectionId);
        if (anomaly == null)
        {
            throw new KeyNotFoundException($"Detected anomaly with ID '{request.DetectionId}' was not found.");
        }

        if (anomaly.Media == null || anomaly.Media.MissionId != request.MissionId)
        {
            throw new KeyNotFoundException($"Detected anomaly with ID '{request.DetectionId}' was not found in mission '{request.MissionId}'.");
        }

        var decision = NormalizeDecision(request.Decision);
        if (decision == "Accepted")
        {
            anomaly.Confirm(_currentUser.UserId, request.Notes);
        }
        else
        {
            anomaly.Reject(_currentUser.UserId, request.Notes);
        }

        await _anomalyRepository.UpdateAsync(anomaly);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MissionAiDetectionMapper.MapDetection(anomaly);
    }

    private static string NormalizeDecision(string decision)
    {
        if (string.Equals(decision, "Accepted", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "Accept", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "Confirm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            return "Accepted";
        }

        if (string.Equals(decision, "Rejected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "Reject", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "Denied", StringComparison.OrdinalIgnoreCase))
        {
            return "Rejected";
        }

        throw new ArgumentException("Decision must be either 'Accepted' or 'Rejected'.", nameof(decision));
    }
}
