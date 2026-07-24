using System.Threading.Tasks;
using UavPms.IdentityService.Domain.Entities;

namespace UavPms.IdentityService.Domain.Interfaces.Repositories;

public interface IUavRepository : IGenericRepository<Uav>
{
    Task<Uav?> GetByUavCodeAsync(string code);
}
