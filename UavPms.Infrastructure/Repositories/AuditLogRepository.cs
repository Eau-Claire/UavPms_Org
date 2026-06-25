using Microsoft.EntityFrameworkCore;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Infrastructure.Persistence;

namespace UavPms.Infrastructure.Repositories;

public class AuditLogRepository : GenericRepository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetAuditLogsPagedAsync(int page, int pageSize, string? search, string? tableName, string? actionType)
    {
        var query = _context.AuditLogs
            .Include(x => x.User)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(tableName))
        {
            var lowerTable = tableName.ToLower();
            query = query.Where(x => x.TableName.ToLower() == lowerTable);
        }

        if (!string.IsNullOrEmpty(actionType))
        {
            var lowerAction = actionType.ToLower();
            query = query.Where(x => x.ActionType.ToLower() == lowerAction);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(x =>
                x.IpAddress.ToLower().Contains(lowerSearch) || x.UserAgent.ToLower().Contains(lowerSearch)
                                                            || x.OldValues.ToLower().Contains(lowerSearch) || x
                                                                .NewValues.ToLower().Contains(lowerSearch)
                                                                    || (x.User != null && x.User.Username.ToLower()
                                                                        .Contains(lowerSearch)));
        }
        
        var totalCaount = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (items, totalCaount);
    }
}