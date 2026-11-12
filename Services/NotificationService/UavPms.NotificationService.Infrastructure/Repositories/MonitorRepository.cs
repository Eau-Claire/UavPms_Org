using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UavPms.NotificationService.Domain.Interfaces.Repositories;
using UavPms.NotificationService.Domain.Models.Monitor;
using UavPms.NotificationService.Infrastructure.Persistence;

namespace UavPms.NotificationService.Infrastructure.Repositories;

public class MonitorRepository : IMonitorRepository
{
    private static readonly string[] PendingMissionStatuses =
        ["Draft", "PendingAcceptance", "Assigned", "Preparing", "Ready", "Postponed"];

    private static readonly string[] InProgressMissionStatuses =
        ["InProgress", "Suspended"];

    private readonly ApplicationDbContext _context;

    public MonitorRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ActiveAlertModel>> GetActiveAlertsAsync(CancellationToken cancellationToken)
    {
        return await _context.Notifications
            .AsNoTracking()
            .Where(n => n.ReadAt == null)
            .OrderByDescending(n => n.SentAt)
            .Select(n => new ActiveAlertModel
            {
                NotificationId = n.Id,
                Title = n.Title,
                Message = n.Body,
                CreatedAt = n.SentAt,
                IsRead = n.ReadAt != null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MissionStatusOverviewModel> GetMissionStatusOverviewAsync(CancellationToken cancellationToken)
    {
        var counts = await _context.Missions
            .AsNoTracking()
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new MissionStatusOverviewModel
        {
            Pending = counts.Where(c => PendingMissionStatuses.Contains(c.Status, StringComparer.OrdinalIgnoreCase)).Sum(c => c.Count),
            InProgress = counts.Where(c => InProgressMissionStatuses.Contains(c.Status, StringComparer.OrdinalIgnoreCase)).Sum(c => c.Count),
            Completed = counts.Where(c => string.Equals(c.Status, "Completed", StringComparison.OrdinalIgnoreCase)).Sum(c => c.Count)
        };
    }

    public async Task<MonitorSummaryModel> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var overview = await GetMissionStatusOverviewAsync(cancellationToken);
        var totalMissions = await _context.Missions.AsNoTracking().CountAsync(cancellationToken);

        var totalInspections = 0;
        var totalDefects = 0;
        var criticalDefects = 0;

        if (_context.Database.IsRelational())
        {
            var conn = _context.Database.GetDbConnection();
            var shouldClose = conn.State != ConnectionState.Open;
            if (shouldClose) await conn.OpenAsync(cancellationToken);
            try
            {
                totalInspections = await ExecuteScalarIntAsync(
                    conn,
                    """SELECT COUNT(*)::int FROM "InspectionMedia" WHERE NOT "IsDeleted" """,
                    cancellationToken);

                totalDefects = await ExecuteScalarIntAsync(
                    conn,
                    """SELECT COUNT(*)::int FROM "DetectedAnomalies" WHERE NOT "IsDeleted" AND "SupersededById" IS NULL """,
                    cancellationToken);

                criticalDefects = await ExecuteScalarIntAsync(
                    conn,
                    """
                    SELECT COUNT(*)::int
                    FROM "DetectedAnomalies" a
                    INNER JOIN "DefectCategories" c ON a."CategoryId" = c."Id"
                    WHERE NOT a."IsDeleted" AND a."SupersededById" IS NULL AND c."IsEmergencyClass" = TRUE
                    """,
                    cancellationToken);
            }
            finally
            {
                if (shouldClose) await conn.CloseAsync();
            }
        }

        return new MonitorSummaryModel
        {
            TotalMissions = totalMissions,
            PendingMissions = overview.Pending,
            InProgressMissions = overview.InProgress,
            CompletedMissions = overview.Completed,
            TotalInspections = totalInspections,
            TotalDefects = totalDefects,
            CriticalDefects = criticalDefects
        };
    }

    public async Task<DefectStatisticsModel> GetDefectStatisticsAsync(CancellationToken cancellationToken)
    {
        var model = new DefectStatisticsModel();
        if (!_context.Database.IsRelational())
            return model;

        var conn = _context.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync(cancellationToken);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT c."CategoryName", COUNT(*)::int AS cnt
                FROM "DetectedAnomalies" a
                INNER JOIN "DefectCategories" c ON a."CategoryId" = c."Id"
                WHERE NOT a."IsDeleted" AND a."SupersededById" IS NULL
                GROUP BY c."CategoryName"
                ORDER BY cnt DESC
                """;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var count = reader.GetInt32(1);
                model.ByType.Add(new DefectStatisticsModel.DefectTypeStatModel
                {
                    DefectType = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0),
                    Count = count
                });
                model.TotalDefects += count;
            }
        }
        finally
        {
            if (shouldClose) await conn.CloseAsync();
        }

        return model;
    }

    public async Task<(List<RecentDefectModel> Items, int TotalCount)> GetRecentDefectsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
            return (new List<RecentDefectModel>(), 0);

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var items = new List<RecentDefectModel>();
        var total = 0;

        var conn = _context.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync(cancellationToken);
        try
        {
            total = await ExecuteScalarIntAsync(
                conn,
                """SELECT COUNT(*)::int FROM "DetectedAnomalies" WHERE NOT "IsDeleted" AND "SupersededById" IS NULL """,
                cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT a."Id", m."MissionId", ms."Title", a."ImageUrl", c."CategoryName", a."CreatedAt"
                FROM "DetectedAnomalies" a
                INNER JOIN "InspectionMedia" m ON a."MediaId" = m."Id"
                LEFT JOIN "Missions" ms ON m."MissionId" = ms."Id"
                INNER JOIN "DefectCategories" c ON a."CategoryId" = c."Id"
                WHERE NOT a."IsDeleted" AND a."SupersededById" IS NULL
                ORDER BY a."CreatedAt" DESC
                LIMIT @limit OFFSET @offset
                """;
            AddParam(cmd, "@limit", pageSize);
            AddParam(cmd, "@offset", (page - 1) * pageSize);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new RecentDefectModel
                {
                    InspectionId = reader.GetGuid(0),
                    MissionId = reader.GetGuid(1),
                    MissionTitle = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    ImageUrl = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    DefectType = reader.IsDBNull(4) ? "Defect" : reader.GetString(4),
                    DetectedAt = reader.GetDateTime(5)
                });
            }
        }
        finally
        {
            if (shouldClose) await conn.CloseAsync();
        }

        return (items, total);
    }

    public async Task<(List<InspectionHistoryModel> Items, int TotalCount)> GetInspectionHistoryAsync(
        Guid? missionId,
        bool? isDefect,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
            return (new List<InspectionHistoryModel>(), 0);

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var items = new List<InspectionHistoryModel>();
        var total = 0;

        var conn = _context.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose) await conn.OpenAsync(cancellationToken);
        try
        {
            var conditions = new List<string> { "NOT m.\"IsDeleted\"" };
            if (missionId.HasValue && missionId.Value != Guid.Empty) conditions.Add("m.\"MissionId\" = @missionId");
            if (fromDate.HasValue) conditions.Add("m.\"CapturedAt\" >= @fromDate");
            if (toDate.HasValue) conditions.Add("m.\"CapturedAt\" <= @toDate");
            if (isDefect == true) conditions.Add("a.\"Id\" IS NOT NULL");
            if (isDefect == false) conditions.Add("a.\"Id\" IS NULL");

            var whereSql = string.Join(" AND ", conditions);

            await using (var countCmd = conn.CreateCommand())
            {
                countCmd.CommandText = $"""
                    SELECT COUNT(DISTINCT m."Id")::int
                    FROM "InspectionMedia" m
                    LEFT JOIN "DetectedAnomalies" a ON a."MediaId" = m."Id" AND NOT a."IsDeleted" AND a."SupersededById" IS NULL
                    WHERE {whereSql}
                    """;
                AddFilterParams(countCmd, missionId, fromDate, toDate);
                var scalar = await countCmd.ExecuteScalarAsync(cancellationToken);
                total = scalar is int i ? i : Convert.ToInt32(scalar ?? 0);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT DISTINCT ON (m."Id")
                    m."Id", m."MissionId", ms."Title", m."FileUrl",
                    (a."Id" IS NOT NULL) AS is_defect,
                    c."CategoryName", m."CapturedAt"
                FROM "InspectionMedia" m
                LEFT JOIN "Missions" ms ON m."MissionId" = ms."Id"
                LEFT JOIN "DetectedAnomalies" a ON a."MediaId" = m."Id" AND NOT a."IsDeleted" AND a."SupersededById" IS NULL
                LEFT JOIN "DefectCategories" c ON a."CategoryId" = c."Id"
                WHERE {whereSql}
                ORDER BY m."Id", a."CreatedAt" DESC NULLS LAST
                LIMIT @limit OFFSET @offset
                """;
            AddFilterParams(cmd, missionId, fromDate, toDate);
            AddParam(cmd, "@limit", pageSize);
            AddParam(cmd, "@offset", (page - 1) * pageSize);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new InspectionHistoryModel
                {
                    InspectionId = reader.GetGuid(0),
                    MissionId = reader.GetGuid(1),
                    MissionTitle = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    ImageUrl = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    IsDefect = !reader.IsDBNull(4) && reader.GetBoolean(4),
                    DefectType = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    DetectedAt = reader.GetDateTime(6)
                });
            }
        }
        finally
        {
            if (shouldClose) await conn.CloseAsync();
        }

        return (items, total);
    }

    private static async Task<int> ExecuteScalarIntAsync(DbConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int count ? count : Convert.ToInt32(result ?? 0);
    }

    private static void AddFilterParams(DbCommand cmd, Guid? missionId, DateTime? fromDate, DateTime? toDate)
    {
        if (missionId.HasValue && missionId.Value != Guid.Empty) AddParam(cmd, "@missionId", missionId.Value);
        if (fromDate.HasValue) AddParam(cmd, "@fromDate", fromDate.Value);
        if (toDate.HasValue) AddParam(cmd, "@toDate", toDate.Value);
    }

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
