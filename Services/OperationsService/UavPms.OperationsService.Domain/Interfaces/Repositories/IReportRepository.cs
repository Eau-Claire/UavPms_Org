using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IReportRepository : IGenericRepository<Report>
{
    Task<(IReadOnlyList<Report> Items, int TotalCount)> GetReportsPagedAsync(
        int page,
        int pageSize,
        ReportType? type = null,
        ReportStatus? status = null,
        Guid? transmissionLineId = null,
        Guid? substationId = null,
        string? searchTerm = null,
        string? sortBy = null,
        string? sortOrder = null
    );

    Task<Report?> GetReportByIdWithDetailsAsync(Guid id);

    Task<string> GetNextReportCodeAsync(int year);

    Task<int> CountAnomaliesByMissionIdsAsync(IEnumerable<Guid> missionIds);

    Task<IReadOnlyList<DetectedAnomaly>> GetAnomaliesByMissionIdsAsync(IEnumerable<Guid> missionIds);

    Task<bool> CanUserManageReportAsync(Guid userId, Report report);

    Task<(int Total, Dictionary<string, int> ByType, Dictionary<string, int> ByStatus)> GetReportStatisticsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null
    );
}
