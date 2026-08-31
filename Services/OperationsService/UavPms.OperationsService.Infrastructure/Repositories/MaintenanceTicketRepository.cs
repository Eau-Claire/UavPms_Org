using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class MaintenanceTicketRepository : GenericRepository<MaintenanceTicket>, IMaintenanceTicketRepository
{
    public MaintenanceTicketRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<MaintenanceTicket?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _context.MaintenanceTickets
            .Include(t => t.Tower)
            .Include(t => t.Component)
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Media)
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Category)
            .Include(t => t.Manager)
            .Include(t => t.Technician)
            .Include(t => t.MaterialLogs)
            .Include(t => t.MaintenanceProofs)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<MaintenanceTicket?> GetByCodeWithDetailsAsync(string ticketCode)
    {
        return await _context.MaintenanceTickets
            .Include(t => t.Tower)
            .Include(t => t.Component)
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Media)
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Category)
            .Include(t => t.Manager)
            .Include(t => t.Technician)
            .Include(t => t.MaterialLogs)
            .Include(t => t.MaintenanceProofs)
            .FirstOrDefaultAsync(t => t.TicketCode == ticketCode);
    }

    public async Task<IReadOnlyList<MaintenanceTicket>> GetAllWithDetailsAsync()
    {
        return await _context.MaintenanceTickets
            .Include(t => t.Tower)
            .Include(t => t.Component)
            .Include(t => t.Anomaly)
            .Include(t => t.Manager)
            .Include(t => t.Technician)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<MaintenanceTicket>> GetByTechnicianIdWithDetailsAsync(Guid technicianId)
    {
        return await _context.MaintenanceTickets
            .Include(t => t.Tower)
            .Include(t => t.Component)
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Category)
            .Include(t => t.Manager)
            .Include(t => t.Technician)
            .Where(t => t.TechnicianId == technicianId)
            .ToListAsync();
    }
}
