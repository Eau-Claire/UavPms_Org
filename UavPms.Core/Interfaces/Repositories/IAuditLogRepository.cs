using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Repositories;

public interface IAuditLogRepository : IGenericRepository<AuditLog>
{
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetAuditLogsPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? tableName,
        string? actionType);
}