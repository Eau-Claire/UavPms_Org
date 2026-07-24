using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Domain.Interfaces.Repositories;

public interface IInspectionMediaRepository : IGenericRepository<InspectionMedia>
{
    Task<InspectionMedia?> GetByIdWithDetailsAsync(Guid id);
    Task<IReadOnlyList<InspectionMedia>> GetByMissionIdWithDetailsAsync(Guid missionId);
}
