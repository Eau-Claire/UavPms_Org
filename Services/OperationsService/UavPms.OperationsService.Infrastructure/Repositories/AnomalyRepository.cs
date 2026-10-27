using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class AnomalyRepository : GenericRepository<DetectedAnomaly>, IAnomalyRepository
{
    public AnomalyRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<DetectedAnomaly?> GetByIdWithDetailAsync(Guid id)
    {
        return await _context.DetectedAnomalies
            .Include(a => a.Media)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.Tower)
            .Include(a => a.AnalysisRun)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetAllWithDetailsAsync()
    {
        return await _context.DetectedAnomalies
            .Include(a => a.Media)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.Tower)
            .Include(a => a.AnalysisRun)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetPendingWithDetailsAsync()
    {
        return await _context.DetectedAnomalies
            .Include(a => a.Media)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.Tower)
            .Include(a => a.AnalysisRun)
            .Where(a => a.ValidationStatus == "Pending")
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetByTowerIdWithDetailsAsync(Guid towerId)
    {
        return await _context.DetectedAnomalies
            .AsNoTracking()
            .Include(a => a.Media)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.AnalysisRun)
            .Where(a => a.TowerId == towerId && a.SupersededById == null)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }
}