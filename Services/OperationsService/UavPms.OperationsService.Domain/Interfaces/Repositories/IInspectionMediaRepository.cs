using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IInspectionMediaRepository : IGenericRepository<InspectionMedia>
{
    Task<InspectionMedia?> GetByIdWithDetailsAsync(Guid id);
    Task<IReadOnlyList<InspectionMedia>> GetByMissionIdWithDetailsAsync(Guid missionId);
}
