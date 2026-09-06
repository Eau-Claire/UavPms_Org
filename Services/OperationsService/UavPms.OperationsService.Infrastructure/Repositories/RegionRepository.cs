using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class RegionRepository : GenericRepository<Region>, IRegionRepository
{
    public RegionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<Region> Items, int TotalCount)> GetRegionsPagedAsync(
        int page,
        int pageSize,
        string? searchTerm)
    {
        var query = ReadQuery.Where(r => !r.IsDeleted);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(r => r.RegionName.Contains(searchTerm));
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(r => r.RegionName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}