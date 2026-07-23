using Microsoft.EntityFrameworkCore;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Infrastructure.Persistence;

namespace UavPms.IdentityService.Infrastructure.Repositories;

public class SubstationRepository : GenericRepository<Substation>, ISubstationRepository
{
    public SubstationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<Substation> Items, int TotalCount)> GetSubstationsPagedAsync(int page, int pageSize, Guid? regionAssetId, string? searchTerm)
    {
        var query = _context.Substations.Where(s => !s.IsDeleted);

        if (regionAssetId.HasValue)
        {
            query = query.Where(s => s.RegionAssetId == regionAssetId.Value);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(s => s.SubstationName.Contains(searchTerm));
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(s => s.SubstationName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (items, totalCount);
    }
}