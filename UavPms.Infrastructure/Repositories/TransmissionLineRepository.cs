using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Infrastructure.Persistence;

namespace UavPms.Infrastructure.Repositories;

public class TransmissionLineRepository : GenericRepository<TransmissionLine>, ITransmissionLineRepository
{
    public TransmissionLineRepository(ApplicationDbContext context) : base(context)
    {
    }
}