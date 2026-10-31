using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;

namespace UavPms.OperationsService.Application.Features.Maintenance;

public interface IMaintenanceTicketService
{
    Task<MaintenanceTicketDto> CreateTicketAsync(CreateMaintenanceTicketRequest request, CancellationToken ct = default);
    Task<MaintenanceTicketDto> AssignTechnicianAsync(Guid ticketId, AssignTechnicianRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TechnicianSummaryDto>> GetTechniciansAsync(CancellationToken ct = default);
    Task<MaintenanceTicketDto> UpdateTicketStatusAsync(Guid ticketId, string newStatus, CancellationToken ct = default);
    Task<MaintenanceProofDto> UploadProofAsync(Guid ticketId, IFormFile file, string? technicianNotes, CancellationToken ct = default);
    Task<MaterialLogDto> LogMaterialAsync(Guid ticketId, LogMaterialRequest request, CancellationToken ct = default);
    Task<MaintenanceTicketDto> CloseTicketAsync(Guid ticketId, CancellationToken ct = default);
    Task<IReadOnlyList<MaintenanceTicketDto>> GetMyTicketsAsync(CancellationToken ct = default);
    Task<PagedTicketsDto> GetTicketsAsync(int pageIndex, int pageSize, string? status, string? priority, CancellationToken ct = default);
    Task<MaintenanceTicketDto> GetTicketByIdAsync(Guid ticketId, CancellationToken ct = default);
}
