using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
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
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Tower)
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
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Tower)
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
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Tower)
            .Include(t => t.Anomaly)
            .Include(t => t.Manager)
            .Include(t => t.Technician)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<MaintenanceTicket>> GetByTechnicianIdWithDetailsAsync(Guid technicianId)
    {
        return await _context.MaintenanceTickets
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Tower)
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Category)
            .Include(t => t.Manager)
            .Include(t => t.Technician)
            .Where(t => t.TechnicianId == technicianId)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<MaintenanceTicket> Items, int TotalCount)> GetByTowerIdPagedAsync(
        Guid towerId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.MaintenanceTickets.AsNoTracking().Where(t => t.Anomaly!.TowerId == towerId);
        if (from.HasValue) query = query.Where(t => t.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(t => t.CreatedAt <= to.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(t => t.Anomaly!)
                .ThenInclude(a => a.Category)
            .Include(t => t.MaintenanceProofs)
            .Include(t => t.MaterialLogs)
            .OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyDictionary<TicketStatus, int>> CountByTowerGroupedByStatusAsync(Guid towerId, CancellationToken cancellationToken = default)
    {
        return await _context.MaintenanceTickets.AsNoTracking()
            .Where(t => t.Anomaly!.TowerId == towerId)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
    }
}
