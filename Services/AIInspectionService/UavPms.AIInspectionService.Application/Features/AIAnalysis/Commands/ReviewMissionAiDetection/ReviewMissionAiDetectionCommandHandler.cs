using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using UavPms.AIInspectionService.Application.Common.Exceptions;
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
    private readonly IGenericRepository<EmergencyAlert>? _alertRepository;
    private readonly IGenericRepository<MaintenanceTicket>? _ticketRepository;
    private readonly IGenericRepository<Mission>? _missionRepository;

    [ActivatorUtilitiesConstructor]
    public ReviewMissionAiDetectionCommandHandler(
        IAnomalyRepository anomalyRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserServices currentUser,
        IGenericRepository<OutboxMessage> outboxRepository,
        IEventPublisher eventPublisher,
        IGenericRepository<EmergencyAlert>? alertRepository = null,
        IGenericRepository<MaintenanceTicket>? ticketRepository = null,
        IGenericRepository<Mission>? missionRepository = null)
    {
        _anomalyRepository = anomalyRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _outboxRepository = outboxRepository;
        _eventPublisher = eventPublisher;
        _alertRepository = alertRepository;
        _ticketRepository = ticketRepository;
        _missionRepository = missionRepository;
    }

    public ReviewMissionAiDetectionCommandHandler(
        IAnomalyRepository anomalyRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserServices currentUser)
        : this(anomalyRepository, unitOfWork, currentUser, null!, null!, null, null, null) { }

    public async Task<MissionAiDetectionDto> Handle(
        ReviewMissionAiDetectionCommand request,
        CancellationToken cancellationToken)
    {
        var anomaly = await _anomalyRepository.GetByIdWithDetailAsync(request.DetectionId);
        if (anomaly == null)
        {
            throw new KeyNotFoundException($"Detected anomaly with ID '{request.DetectionId}' was not found.");
        }

        if (request.MissionId != Guid.Empty && (anomaly.Media == null || anomaly.Media.MissionId != request.MissionId))
        {
            throw new KeyNotFoundException($"Detected anomaly with ID '{request.DetectionId}' was not found in mission '{request.MissionId}'.");
        }

        if (anomaly.SupersededById.HasValue)
        {
            throw new BusinessRuleException("ANOMALY_SUPERSEDED",
                "Superseded detections cannot be reviewed.");
        }

        if (!anomaly.IsAwaitingReview)
        {
            throw new BusinessRuleException("ANOMALY_NOT_IN_REVIEW",
                $"Detection is '{anomaly.ValidationStatus}'; only detections in review can be confirmed or rejected.");
        }

        if (!DetectedAnomaly.TryParseReviewDecision(request.Decision, out var isApproved))
        {
            throw new BusinessRuleException("INVALID_REVIEW_STATUS",
                "Decision must be either 'Confirmed' ('Accepted'/'Approved') or 'Rejected'.");
        }

        var publishConfirmed = isApproved && anomaly.ValidationStatus != DetectedAnomaly.StatusConfirmed;
        string? createdTaskId = null;
        string? emergencyAlertId = null;

        if (isApproved)
        {
            // ERD v2.2: an anomaly must be linked to a tower before it can be Confirmed.
            anomaly.TowerId ??= request.TowerId ?? anomaly.Media?.TowerId;
            if (anomaly.TowerId == null)
                throw new BusinessRuleException("TOWER_REQUIRED", "The detection must be linked to a tower before it can be confirmed.");

            anomaly.Confirm(_currentUser.UserId, request.Notes, request.AnalystFeedbackJson);

            var isEmergency = anomaly.IsEmergencySeverity(request.OverrideSeverity);
            var now = DateTime.UtcNow;

            // 1. Ensure Active EmergencyAlert exists when emergency
            if (isEmergency && _alertRepository != null)
            {
                var existingAlerts = await _alertRepository.FindAsync(
                    a => a.AnomalyId == anomaly.Id && a.Status == "Active" && !a.IsDeleted);
                var existingAlert = existingAlerts?.FirstOrDefault();
                if (existingAlert == null)
                {
                    var alert = new EmergencyAlert
                    {
                        Id = Guid.NewGuid(),
                        AnomalyId = anomaly.Id,
                        Status = "Active",
                        Priority = "Emergency",
                        TriggeredAt = now,
                        ReceivedAt = now,
                        Notes = $"Auto-generated on anomaly review: {request.Notes ?? anomaly.AnalystNotes}",
                        CreatedAt = now
                    };
                    await _alertRepository.AddAsync(alert);
                    emergencyAlertId = alert.Id.ToString();
                }
                else
                {
                    emergencyAlertId = existingAlert.Id.ToString();
                }
            }

            // 2. Ensure Open MaintenanceTicket exists (unassigned technician; Manager assigns later in MF04)
            if (_ticketRepository != null)
            {
                var existingTickets = await _ticketRepository.FindAsync(
                    t => t.AnomalyId == anomaly.Id && !t.IsDeleted);
                var existingTicket = existingTickets?.FirstOrDefault();
                if (existingTicket == null)
                {
                    var missionId = request.MissionId != Guid.Empty
                        ? request.MissionId
                        : (anomaly.Media?.MissionId ?? Guid.Empty);
                    var mission = (_missionRepository != null && missionId != Guid.Empty)
                        ? await _missionRepository.GetByIdAsync(missionId, false, cancellationToken)
                        : null;
                    var managerId = mission?.ManagerId ?? _currentUser.UserId;

                    var priority = isEmergency
                        ? MaintenanceTicket.PriorityEmergency
                        : (string.Equals(request.OverrideSeverity, "Low", StringComparison.OrdinalIgnoreCase)
                            ? MaintenanceTicket.PriorityLow
                            : MaintenanceTicket.PriorityHigh);

                    var ticket = new MaintenanceTicket
                    {
                        Id = Guid.NewGuid(),
                        TicketCode = $"TKT-{now:yyyyMMdd}-{RandomNumberGenerator.GetInt32(1000, 10000)}",
                        AnomalyId = anomaly.Id,
                        ManagerId = managerId,
                        TechnicianId = null,
                        AssignedAt = null,
                        Status = MaintenanceTicket.StatusOpen,
                        Priority = priority,
                        Description = $"[AI Review] {anomaly.Category?.CategoryName ?? "Defect"}: {request.Notes ?? anomaly.AnalystNotes}",
                        DueDate = now.AddDays(isEmergency ? 1 : 7),
                        CreatedAt = now
                    };
                    await _ticketRepository.AddAsync(ticket);
                    createdTaskId = ticket.Id.ToString();
                }
                else
                {
                    createdTaskId = existingTicket.Id.ToString();
                }
            }

            if (publishConfirmed && _outboxRepository != null)
            {
                var confirmedEvent = CreateConfirmedEvent(anomaly);
                await _outboxRepository.AddAsync(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    MessageType = nameof(DefectDetectedEvent),
                    Payload = JsonSerializer.Serialize(confirmedEvent),
                    OccurredAt = now
                });
            }
        }
        else
        {
            anomaly.Reject(_currentUser.UserId, request.Notes, request.AnalystFeedbackJson);

            // Dismiss any Active EmergencyAlert for a rejected false-positive anomaly
            if (_alertRepository != null)
            {
                var activeAlerts = await _alertRepository.FindAsync(
                    a => a.AnomalyId == anomaly.Id && a.Status == "Active" && !a.IsDeleted, track: true);
                if (activeAlerts != null)
                {
                    var now = DateTime.UtcNow;
                    foreach (var activeAlert in activeAlerts)
                    {
                        activeAlert.Status = "Dismissed";
                        activeAlert.ResolvedAt = now;
                        activeAlert.ReviewedById = _currentUser.UserId;
                        if (!string.IsNullOrWhiteSpace(request.Notes))
                        {
                            activeAlert.Notes = request.Notes;
                        }
                        activeAlert.UpdatedAt = now;
                        await _alertRepository.UpdateAsync(activeAlert);
                    }
                }
            }
        }

        await _anomalyRepository.UpdateAsync(anomaly);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (publishConfirmed && _outboxRepository == null && _eventPublisher != null)
            await _eventPublisher.PublishAsync(CreateConfirmedEvent(anomaly));

        var dto = MissionAiDetectionMapper.MapDetection(anomaly);
        dto.MaintenanceTaskId = createdTaskId;
        dto.EmergencyAlertId = emergencyAlertId;
        return dto;
    }

    private static DefectDetectedEvent CreateConfirmedEvent(DetectedAnomaly anomaly) => new()
    {
        InspectionId = anomaly.Media?.MissionId ?? Guid.Empty,
        RecordId = anomaly.Id,
        MissionId = anomaly.Media?.MissionId ?? Guid.Empty,
        ImageUrl = anomaly.ImageUrl ?? anomaly.Media?.FileUrl ?? string.Empty,
        IsDefect = true,
        DefectType = anomaly.Category?.CategoryName ?? anomaly.CategoryId.ToString(),
        DetectedAt = anomaly.ValidatedAt ?? DateTime.UtcNow
    };
}
