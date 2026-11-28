using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IUavRepository : IGenericRepository<Uav>
{
    Task<Uav?> GetByUavCodeAsync(string code);

    /// <summary>Drone người gọi được xem (theo vùng hoặc nhiệm vụ được giao; SystemAdmin: tất cả).</summary>
    Task<IReadOnlyList<Uav>> GetVisibleAsync(CancellationToken ct = default);

    /// <summary>Drone có nằm trong phạm vi người gọi được xem không.</summary>
    Task<bool> IsVisibleAsync(Guid id, CancellationToken ct = default);
}
