using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IUavRepository : IGenericRepository<Uav>
{
    Task<Uav?> GetByUavCodeAsync(string code);
}
