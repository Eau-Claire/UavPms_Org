using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Infrastructure.Persistence;

namespace UavPms.Infrastructure.Repositories;

public class RegionRepository : GenericRepository<Region>, IRegionRepository
{
    public RegionRepository(ApplicationDbContext context) : base(context)
    {
    }
}