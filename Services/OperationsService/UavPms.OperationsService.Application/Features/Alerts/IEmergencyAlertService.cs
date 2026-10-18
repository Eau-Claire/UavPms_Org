using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Application.Features.Alerts.DTOs;

namespace UavPms.OperationsService.Application.Features.Alerts;

public interface IEmergencyAlertService
{
    Task<List<ActiveEmergencyAlertDto>> GetActiveAlertsAsync(CancellationToken ct = default);
    Task<PaginatedAlertsResponse> GetAlertHistoryAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<EmergencyAlertDetailDto?> GetAlertByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ReviewAlertAsync(Guid id, Guid reviewerId, ReviewEmergencyAlertRequest request, CancellationToken ct = default);
    Task<AlertEscalationDto> EscalateAlertAsync(Guid id, Guid currentUserId, EscalateEmergencyAlertRequest request, CancellationToken ct = default);
    Task<EmergencyAlertDetailDto> TriggerAlertAsync(TriggerEmergencyAlertRequest request, CancellationToken ct = default);
}
