using System.Threading.Tasks;
using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Domain.Interfaces.Repositories;

public interface IUavRepository : IGenericRepository<Uav>
{
    Task<Uav?> GetByUavCodeAsync(string code);
}
