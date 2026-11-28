using Microsoft.EntityFrameworkCore;
using UavPms.AIInspectionService.Domain.Common;
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
                .ThenInclude(m => m!.Mission)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.AnalysisRun)
            .Include(a => a.Tower)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetAllWithDetailsAsync(MissionVisibility visibility)
    {
        return await WithDetails(visibility)
            .Where(a => a.SupersededById == null)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetPendingWithDetailsAsync(MissionVisibility visibility)
    {
        return await WithDetails(visibility)
            .Where(a => a.SupersededById == null &&
                        (a.ValidationStatus == DetectedAnomaly.StatusInReview || a.ValidationStatus == DetectedAnomaly.StatusPending))
            .ToListAsync();
    }

    /// <summary>
    /// Defect kèm dữ liệu hiển thị, chỉ gồm nhiệm vụ người gọi được xem (lọc trong SQL): nhiệm vụ mình phụ trách,
    /// được giao, hoặc thuộc vùng của mình (theo vùng nhiệm vụ hoặc vùng của cột điện).
    /// </summary>
    private IQueryable<DetectedAnomaly> WithDetails(MissionVisibility visibility)
    {
        var query = _context.DetectedAnomalies
            .Include(a => a.Media)
                .ThenInclude(m => m!.Mission)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.AnalysisRun)
            .Include(a => a.Tower)
            .AsQueryable();
        if (visibility.All)
            return query;

        var userId = visibility.UserId;
        var regionIds = visibility.RegionIds;
        return query.Where(a =>
            a.Media!.Mission!.ManagerId == userId ||
            a.Media.Mission.Assignments.Any(s => s.UserId == userId && s.Status == MissionAssignment.ActiveStatus) ||
            (a.Media.Mission.RegionId != null && regionIds.Contains(a.Media.Mission.RegionId.Value)) ||
            (a.Tower != null && regionIds.Contains(a.Tower.RegionId)));
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetByTowerIdWithDetailsAsync(Guid towerId)
    {
        return await _context.DetectedAnomalies
            .Include(a => a.Media)
                .ThenInclude(m => m!.Mission)
            .Include(a => a.Category)
            .Include(a => a.Analyst)
            .Include(a => a.AnalysisRun)
            .Include(a => a.Tower)
            .Where(a => a.TowerId == towerId && a.SupersededById == null)
            .ToListAsync();
    }
}