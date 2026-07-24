using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IMaintenanceTicketRepository : IGenericRepository<MaintenanceTicket>
{
    Task<MaintenanceTicket?> GetByIdWithDetailsAsync(Guid id);
    Task<MaintenanceTicket?> GetByCodeWithDetailsAsync(string ticketCode);
    Task<IReadOnlyList<MaintenanceTicket>> GetAllWithDetailsAsync();
    Task<IReadOnlyList<MaintenanceTicket>> GetByTechnicianIdWithDetailsAsync(Guid technicianId);
}