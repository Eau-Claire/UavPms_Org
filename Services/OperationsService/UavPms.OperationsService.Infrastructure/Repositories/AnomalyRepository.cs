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
            .Include(a => a.Asset)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetAllWithDetailsAsync()
    {
        return await _context.DetectedAnomalies
            .Include(a => a.Media)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.Asset)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetPendingWithDetailsAsync()
    {
        return await _context.DetectedAnomalies
            .Include(a => a.Media)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.Asset)
            .Where(a => a.ValidationStatus == "Pending")
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetByAssetIdWithDetailsAsync(Guid assetId)
    {
        return await _context.DetectedAnomalies
            .Include(a => a.Media)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.Asset)
            .Where(a => a.AssetId == assetId)
            .ToListAsync();
    }
}