using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class ReportRepository : GenericRepository<Report>, IReportRepository
{
    public ReportRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<Report> Items, int TotalCount)> GetReportsPagedAsync(
        int page,
        int pageSize,
        ReportType? type = null,
        ReportStatus? status = null,
        Guid? transmissionLineId = null,
        Guid? substationId = null,
        string? searchTerm = null,
        string? sortBy = null,
        string? sortOrder = null)
    {
        var query = ReadQuery
            .Include(r => r.TransmissionLine)
            .Include(r => r.Substation)
            .Include(r => r.CreatedByUser)
            .Where(r => !r.IsDeleted);

        if (type.HasValue)
        {
            query = query.Where(r => r.Type == type.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (transmissionLineId.HasValue)
        {
            query = query.Where(r => r.TransmissionLineId == transmissionLineId.Value);
        }

        if (substationId.HasValue)
        {
            query = query.Where(r => r.SubstationId == substationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(r => r.Title.ToLower().Contains(term) || r.Code.ToLower().Contains(term));
        }

        int totalCount = await query.CountAsync();

        bool isAsc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.ToLowerInvariant()) switch
        {
            "code" => isAsc ? query.OrderBy(r => r.Code) : query.OrderByDescending(r => r.Code),
            "title" => isAsc ? query.OrderBy(r => r.Title) : query.OrderByDescending(r => r.Title),
            "type" => isAsc ? query.OrderBy(r => r.Type) : query.OrderByDescending(r => r.Type),
            "status" => isAsc ? query.OrderBy(r => r.Status) : query.OrderByDescending(r => r.Status),
            _ => isAsc ? query.OrderBy(r => r.CreatedAt) : query.OrderByDescending(r => r.CreatedAt)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Report?> GetReportByIdWithDetailsAsync(Guid id)
    {
        return await ReadQuery
            .Include(r => r.TransmissionLine)
            .Include(r => r.Substation)
            .Include(r => r.ApprovedBy)
            .Include(r => r.CreatedByUser)
            .Include(r => r.ReportMissions)
                .ThenInclude(rm => rm.Mission)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }

    public async Task<string> GetNextReportCodeAsync(int year)
    {
        var prefix = $"BC-{year}-";
        
        // Find existing codes for the year
        var codes = await ReadQuery
            .Where(r => r.Code.StartsWith(prefix))
            .Select(r => r.Code)
            .ToListAsync();

        int maxSeq = 0;
        foreach (var code in codes)
        {
            var parts = code.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int seq))
            {
                if (seq > maxSeq)
                {
                    maxSeq = seq;
                }
            }
        }

        int nextSeq = maxSeq + 1;
        return $"{prefix}{nextSeq:D4}";
    }

    public async Task<int> CountAnomaliesByMissionIdsAsync(IEnumerable<Guid> missionIds)
    {
        var ids = missionIds?.Distinct().ToList();
        if (ids == null || !ids.Any())
        {
            return 0;
        }

        return await _context.DetectedAnomalies
            .Where(a => !a.IsDeleted && a.Media != null && ids.Contains(a.Media.MissionId))
            .CountAsync();
    }

    public async Task<IReadOnlyList<DetectedAnomaly>> GetAnomaliesByMissionIdsAsync(IEnumerable<Guid> missionIds)
    {
        var ids = missionIds?.Distinct().ToList();
        if (ids == null || !ids.Any())
        {
            return Array.Empty<DetectedAnomaly>();
        }

        return await _context.DetectedAnomalies
            .AsNoTracking()
            .Include(a => a.Category)
            .Include(a => a.Media)
                .ThenInclude(m => m!.Mission)
            .Where(a => !a.IsDeleted && a.Media != null && ids.Contains(a.Media.MissionId))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> CanUserManageReportAsync(Guid userId, Report report)
    {
        var hasScopes = await _context.UserGeographicScopes.AnyAsync(s => s.UserId == userId);
        if (!hasScopes)
        {
            return true;
        }

        if (!report.TransmissionLineId.HasValue && !report.SubstationId.HasValue)
        {
            return true;
        }

        var scopes = await _context.UserGeographicScopes
            .Where(s => s.UserId == userId)
            .ToListAsync();

        if (report.SubstationId.HasValue)
        {
            var subId = report.SubstationId.Value;
            var sub = report.Substation ?? await _context.Substations.FirstOrDefaultAsync(s => s.Id == subId);
            if (sub != null)
            {
                if (scopes.Any(s => s.SubstationId == subId || (s.RegionId != null && s.RegionId == sub.RegionAssetId)))
                {
                    return true;
                }
            }
        }

        if (report.TransmissionLineId.HasValue)
        {
            var lineId = report.TransmissionLineId.Value;
            var line = report.TransmissionLine ?? await _context.TransmissionLines.Include(l => l.Substation).FirstOrDefaultAsync(l => l.Id == lineId);
            if (line != null)
            {
                if (scopes.Any(s => s.TransmissionLineId == lineId
                    || (s.SubstationId != null && s.SubstationId == line.SubstationAssetId)
                    || (s.RegionId != null && line.Substation != null && s.RegionId == line.Substation.RegionAssetId)
                    || (s.ManagementUnitId != null && s.ManagementUnitId == line.ManagementUnitId)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public async Task<(int Total, Dictionary<string, int> ByType, Dictionary<string, int> ByStatus)> GetReportStatisticsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        var query = ReadQuery.Where(r => !r.IsDeleted);

        if (from.HasValue)
        {
            var fromUtc = from.Value.UtcDateTime;
            query = query.Where(r => r.CreatedAt >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = to.Value.UtcDateTime;
            query = query.Where(r => r.CreatedAt <= toUtc);
        }

        var reports = await query
            .Select(r => new { r.Type, r.Status })
            .ToListAsync();

        int total = reports.Count;

        var byType = new Dictionary<string, int>
        {
            ["defect"] = reports.Count(r => r.Type == ReportType.Defect),
            ["periodic"] = reports.Count(r => r.Type == ReportType.Periodic),
            ["thermal"] = reports.Count(r => r.Type == ReportType.Thermal),
            ["corridor"] = reports.Count(r => r.Type == ReportType.Corridor)
        };

        var byStatus = new Dictionary<string, int>
        {
            ["approved"] = reports.Count(r => r.Status == ReportStatus.Approved),
            ["pending"] = reports.Count(r => r.Status == ReportStatus.Pending),
            ["draft"] = reports.Count(r => r.Status == ReportStatus.Draft)
        };

        return (total, byType, byStatus);
    }
}
