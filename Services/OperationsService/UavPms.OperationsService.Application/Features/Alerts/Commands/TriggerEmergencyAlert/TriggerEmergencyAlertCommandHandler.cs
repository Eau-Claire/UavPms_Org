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

        Guid? towerId = request.TowerId;
        if (!towerId.HasValue && !string.IsNullOrWhiteSpace(anomaly.TowerId) && Guid.TryParse(anomaly.TowerId, out var parsedTowerId))
        {
            towerId = parsedTowerId;
        }

        string towerCode = string.Empty;
        if (towerId.HasValue)
        {
            var tower = await _towerRepository.GetByIdAsync(towerId.Value);
            if (tower != null)
            {
                towerCode = tower.TowerCode;
                tower.ActiveDefectsCount++;
                tower.RiskLevel = "Critical";
                await _towerRepository.UpdateAsync(tower);
            }
        }

        var alert = new EmergencyAlert
        {
            Id = Guid.NewGuid(),
            AnomalyId = request.AnomalyId,
            AssetId = request.AssetId ?? anomaly.AssetId,
            TowerId = towerId,
            MissionId = request.MissionId,
            Status = "Active",
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Emergency" : request.Priority,
            DeliveryLatencySeconds = 2,
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
            AssetId = alert.AssetId,
            AssetCode = towerCode,
            AlertType = "Emergency Alert",
            CategoryName = anomaly.Category?.CategoryName ?? "Critical Defect",
            Priority = alert.Priority,
            Status = alert.Status,
            DeliveryLatencySeconds = alert.DeliveryLatencySeconds,
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
