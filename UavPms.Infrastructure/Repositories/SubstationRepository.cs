using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Infrastructure.Persistence;

namespace UavPms.Infrastructure.Repositories;

public class SubstationRepository : GenericRepository<Substation>, ISubstationRepository
{
    public SubstationRepository(ApplicationDbContext context) : base(context)
    {
    }
}