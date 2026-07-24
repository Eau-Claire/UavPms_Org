using Microsoft.EntityFrameworkCore;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Infrastructure.Persistence;

namespace UavPms.AIInspectionService.Infrastructure.Repositories;

public class TransmissionLineRepository : GenericRepository<TransmissionLine>, ITransmissionLineRepository
{
    public TransmissionLineRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<TransmissionLine> Items, int TotalCount)> GetTransmissionLinesPagedAsync(int page, int pageSize, Guid? substationAssetId, string? searchTerm)
    {
        var query = _context.TransmissionLines.Where(l => !l.IsDeleted);
        if (substationAssetId.HasValue)
        {
            query = query.Where(l => l.SubstationAssetId == substationAssetId.Value);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(l => l.LineName.Contains(searchTerm));  
        }

        int totalCount = await query.CountAsync();
        
        var items = await query
            .OrderBy(l => l.LineName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (items, totalCount);
    }
}