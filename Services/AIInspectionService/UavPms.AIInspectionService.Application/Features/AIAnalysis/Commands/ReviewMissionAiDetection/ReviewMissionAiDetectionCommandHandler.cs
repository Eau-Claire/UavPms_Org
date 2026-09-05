using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Events;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ReviewMissionAiDetection;

public class ReviewMissionAiDetectionCommandHandler
    : IRequestHandler<ReviewMissionAiDetectionCommand, MissionAiDetectionDto>
{
    private readonly IAnomalyRepository _anomalyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserServices _currentUser;
    private readonly IGenericRepository<OutboxMessage>? _outboxRepository;
    private readonly IEventPublisher? _eventPublisher;

    [ActivatorUtilitiesConstructor]
    public ReviewMissionAiDetectionCommandHandler(
        IAnomalyRepository anomalyRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserServices currentUser,
        IGenericRepository<OutboxMessage> outboxRepository,
        IEventPublisher eventPublisher)
    {
        _anomalyRepository = anomalyRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _outboxRepository = outboxRepository;
        _eventPublisher = eventPublisher;
    }

    public ReviewMissionAiDetectionCommandHandler(
        IAnomalyRepository anomalyRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserServices currentUser)
        : this(anomalyRepository, unitOfWork, currentUser, null!, null!) { }

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
        var publishConfirmed = decision == "Accepted" && anomaly.ValidationStatus != "Confirmed";
        if (decision == "Accepted")
        {
            anomaly.Confirm(_currentUser.UserId, request.Notes ?? string.Empty);
            if (publishConfirmed && _outboxRepository != null)
            {
                var confirmedEvent = CreateConfirmedEvent(anomaly);
                await _outboxRepository.AddAsync(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    MessageType = nameof(DefectDetectedEvent),
                    Payload = JsonSerializer.Serialize(confirmedEvent),
                    OccurredAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _currentUser.UserId
                });
            }
        }
        else
        {
            anomaly.Reject(_currentUser.UserId, request.Notes ?? string.Empty);
        }

        await _anomalyRepository.UpdateAsync(anomaly);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (publishConfirmed && _outboxRepository == null && _eventPublisher != null)
            await _eventPublisher.PublishAsync(CreateConfirmedEvent(anomaly));

        return MissionAiDetectionMapper.MapDetection(anomaly);
    }

    private static DefectDetectedEvent CreateConfirmedEvent(DetectedAnomaly anomaly) => new()
    {
        InspectionId = anomaly.Media!.MissionId,
        RecordId = anomaly.Id,
        MissionId = anomaly.Media.MissionId,
        ImageUrl = anomaly.ImageUrl ?? anomaly.Media.FileUrl,
        IsDefect = true,
        DefectType = anomaly.Category?.CategoryName ?? anomaly.CategoryId.ToString(),
        DetectedAt = anomaly.ValidatedAt ?? DateTime.UtcNow
    };

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
