using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UavPms.IdentityService.Domain.Entities;

namespace UavPms.IdentityService.Domain.Interfaces.Repositories;

public interface IEmergencyAlertRepository : IGenericRepository<EmergencyAlert>
{
    Task<IReadOnlyList<EmergencyAlert>> GetAlertHistoryAsync(
        string? status,
        string? priority,
        DateTime from,
        DateTime to);
}
