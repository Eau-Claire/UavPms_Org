using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Domain.Interfaces.Repositories;

public interface IEmergencyAlertRepository : IGenericRepository<EmergencyAlert>
{
    Task<IReadOnlyList<EmergencyAlert>> GetAlertHistoryAsync(
        string? status,
        string? priority,
        DateTime from,
        DateTime to);
}
