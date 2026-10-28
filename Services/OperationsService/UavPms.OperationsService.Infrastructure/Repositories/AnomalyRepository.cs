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

    public async Task<(IReadOnlyList<DetectedAnomaly> Items, int TotalCount)> GetByTowerIdPagedAsync(
        Guid towerId, string? validationStatus, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.DetectedAnomalies.AsNoTracking()
            .Where(a => a.TowerId == towerId && a.SupersededById == null);
        if (!string.IsNullOrWhiteSpace(validationStatus)) query = query.Where(a => a.ValidationStatus == validationStatus);
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(a => a.CreatedAt <= to.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(a => a.Media)
            .Include(a => a.Category)
            .Include(a => a.AnalysisRun)
            .OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByTowerGroupedByStatusAsync(Guid towerId, CancellationToken cancellationToken = default)
    {
        return await _context.DetectedAnomalies.AsNoTracking()
            .Where(a => a.TowerId == towerId && a.SupersededById == null)
            .GroupBy(a => a.ValidationStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
    }
}