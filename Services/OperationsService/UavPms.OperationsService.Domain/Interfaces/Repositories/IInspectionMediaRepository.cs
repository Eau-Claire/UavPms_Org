using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IInspectionMediaRepository : IGenericRepository<InspectionMedia>
{
    Task<bool> ExistsAsync(Guid id);
    Task<InspectionMedia?> GetByIdWithDetailsAsync(Guid id);
    Task<IReadOnlyList<InspectionMedia>> GetByMissionIdWithDetailsAsync(Guid missionId);
    /// <summary>Capture time of the most recent media of a tower (its last inspection).</summary>
    Task<DateTime?> GetLastCapturedAtByTowerAsync(Guid towerId, CancellationToken cancellationToken = default);
}
