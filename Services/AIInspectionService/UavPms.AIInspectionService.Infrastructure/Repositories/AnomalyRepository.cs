using Microsoft.EntityFrameworkCore;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Infrastructure.Persistence;

namespace UavPms.AIInspectionService.Infrastructure.Repositories;

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
            .Include(a => a.AnalysisRun)
            .Include(a => a.Tower)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetAllWithDetailsAsync()
    {
        return await _context.DetectedAnomalies
            .Include(a => a.Media)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.AnalysisRun)
            .Include(a => a.Tower)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetPendingWithDetailsAsync()
    {
        return await _context.DetectedAnomalies
            .Include(a => a.Media)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.AnalysisRun)
            .Include(a => a.Tower)
            .Where(a => a.ValidationStatus == "Pending")
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetByTowerIdWithDetailsAsync(Guid towerId)
    {
        return await _context.DetectedAnomalies
            .Include(a => a.Media)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.AnalysisRun)
            .Include(a => a.Tower)
            .Where(a => a.TowerId == towerId)
            .ToListAsync();
    }
}