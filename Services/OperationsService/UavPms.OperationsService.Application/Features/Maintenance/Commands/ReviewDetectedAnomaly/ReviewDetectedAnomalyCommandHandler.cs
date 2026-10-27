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

        var isApproved = string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(request.Status, "Confirmed", StringComparison.OrdinalIgnoreCase);
        var isRejected = string.Equals(request.Status, "Rejected", StringComparison.OrdinalIgnoreCase);

        if (!isApproved && !isRejected)
            throw new BusinessRuleException("INVALID_STATUS", "Status must be either 'Confirmed' (or Approved) or 'Rejected'.");

        if (isApproved && !anomaly.TowerId.HasValue)
        {
            // Bất thường phải gắn cột điện trước khi xác nhận; lấy theo media nếu media đã gắn cột.
            var media = anomaly.Media ?? (await _anomalyRepository.GetByIdWithDetailAsync(anomaly.Id))?.Media;
            anomaly.TowerId = media?.TowerId;
            if (!anomaly.TowerId.HasValue)
                throw new BusinessRuleException("TOWER_REQUIRED", "The anomaly must be linked to a tower before it can be confirmed.");
        }

        anomaly.ValidationStatus = isApproved ? "Confirmed" : "Rejected";
        anomaly.AnalystId = _currentUser.UserId;
        anomaly.AnalystNotes = request.ReviewNotes ?? string.Empty;
        anomaly.AnalystFeedback = !string.IsNullOrWhiteSpace(request.AnalystFeedbackJson) ? request.AnalystFeedbackJson : "{}";
        anomaly.ValidatedAt = DateTime.UtcNow;

        string? createdTaskId = null;
        Guid? resolvedTowerId = anomaly.TowerId;

        if (isApproved)
        {
            var isEmergency = string.Equals(request.OverrideSeverity, "Critical", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(request.OverrideSeverity, "Emergency", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(request.OverrideSeverity, "Urgent", StringComparison.OrdinalIgnoreCase) ||
                              (anomaly.Category != null && (anomaly.Category.IsEmergencyClass || anomaly.Category.SeverityWeight >= 4));

            // 1. Create EmergencyAlert if critical
            if (isEmergency)
            {
                var existingAlerts = await _alertRepository.FindAsync(a => a.AnomalyId == anomaly.Id && !a.IsDeleted, track: false);
                if (existingAlerts.Count == 0)
                {
                    var alert = new EmergencyAlert
                    {
                        Id = Guid.NewGuid(),
                        AnomalyId = anomaly.Id,
                        Status = "Active",
                        Priority = "Emergency",
                        TriggeredAt = DateTime.UtcNow,
                        Notes = $"Auto-generated on anomaly review: {request.ReviewNotes}",
                        CreatedAt = DateTime.UtcNow
                    };
                    await _alertRepository.AddAsync(alert);
                }
            }

            // 3. Create MaintenanceTicket if not exists
            var existingTickets = await _ticketRepository.FindAsync(t => t.AnomalyId == anomaly.Id && !t.IsDeleted, track: false);
            if (existingTickets.Count == 0)
            {
                var mission = await _missionRepository.GetByIdAsync(request.MissionId);
                var managerId = mission?.ManagerId ?? _currentUser.UserId;

                var ticket = new MaintenanceTicket
                {
                    Id = Guid.NewGuid(),
                    TicketCode = $"TKT-{DateTime.UtcNow:yyyyMMdd}-{RandomNumberGenerator.GetInt32(1000, 10000)}",
                    AnomalyId = anomaly.Id,
                    ManagerId = managerId,
                    TechnicianId = Guid.Empty,
                    Status = TicketStatus.Open,
                    Priority = isEmergency ? TicketPriority.Emergency : TicketPriority.High,
                    Description = $"[AI Review] {anomaly.Category?.CategoryName ?? "Defect"}: {request.ReviewNotes ?? anomaly.AnalystNotes}",
                    AssignedAt = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(isEmergency ? 1 : 7),
                    CreatedAt = DateTime.UtcNow
                };

                await _ticketRepository.AddAsync(ticket);
                createdTaskId = ticket.Id.ToString();
            }
            else
            {
                createdTaskId = existingTickets[0].Id.ToString();
            }
        }

        await _anomalyRepository.UpdateAsync(anomaly);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ReviewDetectionResultDto
        {
            DetectionId = anomaly.Id.ToString(),
            MissionId = request.MissionId.ToString(),
            Status = anomaly.ValidationStatus,
            ReviewNotes = anomaly.AnalystNotes,
            MaintenanceTaskId = createdTaskId,
            ReviewedAt = anomaly.ValidatedAt.Value
        };
    }
}
