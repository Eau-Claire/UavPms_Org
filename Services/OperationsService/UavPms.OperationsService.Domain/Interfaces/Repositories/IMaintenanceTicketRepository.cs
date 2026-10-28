using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IMaintenanceTicketRepository : IGenericRepository<MaintenanceTicket>
{
    Task<MaintenanceTicket?> GetByIdWithDetailsAsync(Guid id);
    Task<MaintenanceTicket?> GetByCodeWithDetailsAsync(string ticketCode);
    Task<IReadOnlyList<MaintenanceTicket>> GetAllWithDetailsAsync();
    Task<IReadOnlyList<MaintenanceTicket>> GetByTechnicianIdWithDetailsAsync(Guid technicianId);
    /// <summary>Tickets of a tower (through their anomaly), newest first, filtered by creation time and paged in SQL.</summary>
    Task<(IReadOnlyList<MaintenanceTicket> Items, int TotalCount)> GetByTowerIdPagedAsync(
        Guid towerId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<TicketStatus, int>> CountByTowerGroupedByStatusAsync(Guid towerId, CancellationToken cancellationToken = default);
}