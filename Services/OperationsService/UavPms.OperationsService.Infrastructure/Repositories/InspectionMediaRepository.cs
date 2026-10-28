using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class InspectionMediaRepository : GenericRepository<InspectionMedia>, IInspectionMediaRepository
{
    public InspectionMediaRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.InspectionMedia
            .AsNoTracking()
            .AnyAsync(media => media.Id == id);
    }

    public async Task<InspectionMedia?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _context.InspectionMedia
            .Include(m => m.Mission)
            .Include(m => m.Tower)
            .Include(m => m.AnalysisRuns)
            .Include(m => m.DetectedAnomalies)
                .ThenInclude(a => a.Category)
            .Include(m => m.DetectedAnomalies)
                .ThenInclude(a => a.AnalysisRun)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IReadOnlyList<InspectionMedia>> GetByMissionIdWithDetailsAsync(Guid missionId)
    {
        return await _context.InspectionMedia
            .Include(m => m.Mission)
            .Include(m => m.Tower)
            .Include(m => m.AnalysisRuns)
            .Include(m => m.DetectedAnomalies)
                .ThenInclude(a => a.Category)
            .Include(m => m.DetectedAnomalies)
                .ThenInclude(a => a.AnalysisRun)
            .Where(m => m.MissionId == missionId)
            .OrderByDescending(m => m.CapturedAt)
            .ToListAsync();
    }

    public async Task<DateTime?> GetLastCapturedAtByTowerAsync(Guid towerId, CancellationToken cancellationToken = default)
    {
        return await _context.InspectionMedia.AsNoTracking()
            .Where(m => m.TowerId == towerId)
            .MaxAsync(m => (DateTime?)m.CapturedAt, cancellationToken);
    }
}
