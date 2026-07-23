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

    public async Task<InspectionMedia?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _context.InspectionMedia
            .Include(m => m.Mission)
            .Include(m => m.Asset)
            .Include(m => m.DetectedAnomalies)
                .ThenInclude(a => a.Category)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IReadOnlyList<InspectionMedia>> GetByMissionIdWithDetailsAsync(Guid missionId)
    {
        return await _context.InspectionMedia
            .Include(m => m.Mission)
            .Include(m => m.Asset)
            .Include(m => m.DetectedAnomalies)
                .ThenInclude(a => a.Category)
            .Where(m => m.MissionId == missionId)
            .OrderByDescending(m => m.CapturedAt)
            .ToListAsync();
    }
}
