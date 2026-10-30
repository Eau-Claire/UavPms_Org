using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Alerts.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Alerts.Commands.TriggerEmergencyAlert;

public class TriggerEmergencyAlertCommandHandler : IRequestHandler<TriggerEmergencyAlertCommand, ActiveEmergencyAlertDto>
{
    private readonly IGenericRepository<EmergencyAlert> _alertRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IAnomalyRepository _anomalyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TriggerEmergencyAlertCommandHandler(
        IGenericRepository<EmergencyAlert> alertRepository,
        ITowerRepository towerRepository,
        IAnomalyRepository anomalyRepository,
        IUnitOfWork unitOfWork)
    {
        _alertRepository = alertRepository;
        _towerRepository = towerRepository;
        _anomalyRepository = anomalyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ActiveEmergencyAlertDto> Handle(TriggerEmergencyAlertCommand request, CancellationToken cancellationToken)
    {
        var anomaly = await _anomalyRepository.GetByIdAsync(request.AnomalyId);
        if (anomaly == null)
            throw new NotFoundException("DetectedAnomaly", request.AnomalyId);

        // UX_EmergencyAlerts_ActivePerAnomaly: at most one Active alert per anomaly.
        var active = await _alertRepository.FindAsync(a => a.AnomalyId == request.AnomalyId && a.Status == "Active");
        if (active.Count > 0)
            throw new BusinessRuleException("ALERT_ALREADY_ACTIVE",
                $"Anomaly {request.AnomalyId} already has an active emergency alert ({active[0].Id}).");

        // Cột điện của cảnh báo suy ra từ bất thường; nếu bất thường chưa gắn cột thì gắn theo yêu cầu.
        if (!anomaly.TowerId.HasValue && request.TowerId.HasValue)
        {
            anomaly.TowerId = request.TowerId;
            await _anomalyRepository.UpdateAsync(anomaly);
        }

        Guid? towerId = anomaly.TowerId;
        string towerCode = string.Empty;
        if (towerId.HasValue)
        {
            var tower = await _towerRepository.GetByIdAsync(towerId.Value, track: false);
            towerCode = tower?.TowerCode ?? string.Empty;
        }

        var alert = new EmergencyAlert
        {
            Id = Guid.NewGuid(),
            AnomalyId = request.AnomalyId,
            Status = "Active",
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Emergency" : request.Priority,
            TriggeredAt = DateTime.UtcNow,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        await _alertRepository.AddAsync(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ActiveEmergencyAlertDto
        {
            Id = alert.Id,
            AnomalyId = alert.AnomalyId,
            TowerId = towerId,
            TowerCode = towerCode,
            AlertType = "Emergency Alert",
            CategoryName = anomaly.Category?.CategoryName ?? "Critical Defect",
            Priority = alert.Priority,
            Status = alert.Status,
            DeliveryLatencySeconds = 0,
            TriggeredAt = alert.TriggeredAt,
            Location = new AlertLocationDto
            {
                TowerCode = towerCode
            },
            Evidence = new AlertEvidenceDto
            {
                MediaUrl = anomaly.ImageUrl,
                ConfidenceScore = anomaly.ConfidenceScore
            }
        };
    }
}
