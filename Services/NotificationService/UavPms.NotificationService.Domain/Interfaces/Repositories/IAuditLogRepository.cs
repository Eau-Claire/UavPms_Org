using UavPms.NotificationService.Domain.Entities;

namespace UavPms.NotificationService.Domain.Interfaces.Repositories;

public interface IAuditLogRepository : IGenericRepository<AuditLog>
{
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetAuditLogsPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? tableName,
        string? actionType);
}