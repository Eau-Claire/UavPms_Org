using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Alerts.DTOs;

namespace UavPms.OperationsService.Application.Features.Alerts.Commands.TriggerEmergencyAlert;

public record TriggerEmergencyAlertCommand(
    Guid MissionId,
    Guid AnomalyId,
    Guid? TowerId = null,
    Guid? AssetId = null,
    string Priority = "Emergency",
    string? Notes = null
) : IRequest<ActiveEmergencyAlertDto>;
