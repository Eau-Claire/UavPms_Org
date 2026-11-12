using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Maintenance.Commands.ReviewDetectedAnomaly;

public class ReviewDetectedAnomalyCommandHandler : IRequestHandler<ReviewDetectedAnomalyCommand, ReviewDetectionResultDto>
{
    private readonly IAnomalyRepository _anomalyRepository;
    private readonly IMissionRepository _missionRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IGenericRepository<EmergencyAlert> _alertRepository;
    private readonly IGenericRepository<MaintenanceTicket> _ticketRepository;
    private readonly ICurrentUserServices _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewDetectedAnomalyCommandHandler(
        IAnomalyRepository anomalyRepository,
        IMissionRepository missionRepository,
        ITowerRepository towerRepository,
        IGenericRepository<EmergencyAlert> alertRepository,
        IGenericRepository<MaintenanceTicket> ticketRepository,
        ICurrentUserServices currentUser,
        IUnitOfWork unitOfWork)
    {
        _anomalyRepository = anomalyRepository;
        _missionRepository = missionRepository;
        _towerRepository = towerRepository;
        _alertRepository = alertRepository;
        _ticketRepository = ticketRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReviewDetectionResultDto> Handle(ReviewDetectedAnomalyCommand request, CancellationToken cancellationToken)
    {
        var anomaly = await _anomalyRepository.GetByIdAsync(request.AnomalyId);
        if (anomaly == null)
            throw new NotFoundException("DetectedAnomaly", request.AnomalyId);

        if (anomaly.Media == null || anomaly.Category == null)
        {
            var detailed = await _anomalyRepository.GetByIdWithDetailAsync(anomaly.Id);
            if (detailed != null)
            {
                anomaly = detailed;
            }
        }

        if (request.MissionId != Guid.Empty &&
            anomaly.Media != null &&
            anomaly.Media.MissionId != Guid.Empty &&
            anomaly.Media.MissionId != request.MissionId)
        {
            throw new BusinessRuleException("ANOMALY_MISSION_MISMATCH",
                $"The anomaly '{request.AnomalyId}' does not belong to mission '{request.MissionId}'.");
        }

        if (anomaly.SupersededById.HasValue)
        {
            throw new BusinessRuleException("ANOMALY_SUPERSEDED",
                "Superseded anomalies cannot be reviewed.");
        }

        if (!anomaly.IsAwaitingReview)
        {
            throw new BusinessRuleException("ANOMALY_NOT_IN_REVIEW",
                $"Anomaly is '{anomaly.ValidationStatus}'; only anomalies in review can be confirmed or rejected.");
        }

        if (!DetectedAnomaly.TryParseReviewDecision(request.Status, out var isApproved))
        {
            throw new BusinessRuleException("INVALID_STATUS",
                "Status must be either 'Confirmed' (or Approved/Accepted) or 'Rejected'.");
        }

        string? createdTaskId = null;
        string? emergencyAlertId = null;

        if (isApproved)
        {
            // Bất thường phải gắn cột điện trước khi xác nhận; lấy theo request hoặc media nếu chưa gắn cột.
            anomaly.TowerId ??= request.TowerId ?? anomaly.Media?.TowerId;
            if (!anomaly.TowerId.HasValue)
                throw new BusinessRuleException("TOWER_REQUIRED", "The anomaly must be linked to a tower before it can be confirmed.");

            anomaly.Confirm(_currentUser.UserId, request.ReviewNotes, request.AnalystFeedbackJson);

            var isEmergency = anomaly.IsEmergencySeverity(request.OverrideSeverity);
            var now = DateTime.UtcNow;

            // 1. Create EmergencyAlert if critical/emergency
            if (isEmergency)
            {
                var existingAlerts = await _alertRepository.FindAsync(
                    a => a.AnomalyId == anomaly.Id && a.Status == "Active" && !a.IsDeleted, track: false);
                if (existingAlerts.Count == 0)
                {
                    var alert = new EmergencyAlert
                    {
                        Id = Guid.NewGuid(),
                        AnomalyId = anomaly.Id,
                        Status = "Active",
                        Priority = "Emergency",
                        TriggeredAt = now,
                        ReceivedAt = now,
                        Notes = $"Auto-generated on anomaly review: {request.ReviewNotes ?? anomaly.AnalystNotes}",
                        CreatedAt = now
                    };
                    await _alertRepository.AddAsync(alert);
                    emergencyAlertId = alert.Id.ToString();
                }
                else
                {
                    emergencyAlertId = existingAlerts[0].Id.ToString();
                }
            }

            // 2. Create MaintenanceTicket if not exists (unassigned technician; Manager assigns later in MF04)
            var existingTickets = await _ticketRepository.FindAsync(t => t.AnomalyId == anomaly.Id && !t.IsDeleted, track: false);
            if (existingTickets.Count == 0)
            {
                var resolvedMissionId = request.MissionId != Guid.Empty
                    ? request.MissionId
                    : (anomaly.Media?.MissionId ?? Guid.Empty);
                var mission = resolvedMissionId != Guid.Empty
                    ? await _missionRepository.GetByIdAsync(resolvedMissionId)
                    : null;
                var managerId = mission?.ManagerId ?? _currentUser.UserId;

                var priority = isEmergency
                    ? TicketPriority.Emergency
                    : (string.Equals(request.OverrideSeverity, "Low", StringComparison.OrdinalIgnoreCase)
                        ? TicketPriority.Low
                        : TicketPriority.High);

                var ticket = new MaintenanceTicket
                {
                    Id = Guid.NewGuid(),
                    TicketCode = $"TKT-{now:yyyyMMdd}-{RandomNumberGenerator.GetInt32(1000, 10000)}",
                    AnomalyId = anomaly.Id,
                    ManagerId = managerId,
                    TechnicianId = null,
                    AssignedAt = null,
                    Status = TicketStatus.Open,
                    Priority = priority,
                    Description = $"[AI Review] {anomaly.Category?.CategoryName ?? "Defect"}: {request.ReviewNotes ?? anomaly.AnalystNotes}",
                    DueDate = now.AddDays(isEmergency ? 1 : 7),
                    CreatedAt = now
                };

                await _ticketRepository.AddAsync(ticket);
                createdTaskId = ticket.Id.ToString();
            }
            else
            {
                createdTaskId = existingTickets[0].Id.ToString();
            }
        }
        else
        {
            anomaly.Reject(_currentUser.UserId, request.ReviewNotes, request.AnalystFeedbackJson);

            // Dismiss any Active EmergencyAlert for a rejected false-positive anomaly
            var activeAlerts = await _alertRepository.FindAsync(
                a => a.AnomalyId == anomaly.Id && a.Status == "Active" && !a.IsDeleted, track: true);
            var now = DateTime.UtcNow;
            foreach (var activeAlert in activeAlerts)
            {
                activeAlert.Status = "Dismissed";
                activeAlert.ResolvedAt = now;
                activeAlert.ReviewedById = _currentUser.UserId;
                if (!string.IsNullOrWhiteSpace(request.ReviewNotes))
                {
                    activeAlert.Notes = request.ReviewNotes;
                }
                activeAlert.UpdatedAt = now;
                await _alertRepository.UpdateAsync(activeAlert);
            }
        }

        await _anomalyRepository.UpdateAsync(anomaly);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ReviewDetectionResultDto
        {
            DetectionId = anomaly.Id.ToString(),
            MissionId = (request.MissionId != Guid.Empty ? request.MissionId : (anomaly.Media?.MissionId ?? Guid.Empty)).ToString(),
            Status = anomaly.ValidationStatus,
            ReviewNotes = anomaly.AnalystNotes,
            MaintenanceTaskId = createdTaskId,
            EmergencyAlertId = emergencyAlertId,
            ReviewedAt = anomaly.ValidatedAt ?? DateTime.UtcNow
        };
    }
}
