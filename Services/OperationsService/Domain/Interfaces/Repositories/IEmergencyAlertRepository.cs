using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IEmergencyAlertRepository : IGenericRepository<EmergencyAlert>
{
    Task<IReadOnlyList<EmergencyAlert>> GetAlertHistoryAsync(
        string? status,
        string? priority,
        DateTime from,
        DateTime to);
}
