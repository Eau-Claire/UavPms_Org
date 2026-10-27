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
        Guid? regionId = null,
        string? searchTerm = null,
        string? sortBy = null,
        string? sortOrder = null
    );

    Task<Report?> GetReportByIdWithDetailsAsync(Guid id);

    Task<string> GetNextReportCodeAsync(int year);

    /// <summary>Số bất thường trong phạm vi báo cáo, tính trực tiếp (không lưu DefectCount).</summary>
    Task<int> CountAnomaliesForReportAsync(Report report);

    /// <summary>Bất thường trong phạm vi báo cáo: các nhiệm vụ của báo cáo, giới hạn theo DateFrom/DateTo và RegionId.</summary>
    Task<IReadOnlyList<DetectedAnomaly>> GetAnomaliesForReportAsync(Report report);

    Task<bool> CanUserManageReportAsync(Guid userId, Report report);

    Task<(int Total, Dictionary<string, int> ByType, Dictionary<string, int> ByStatus)> GetReportStatisticsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null
    );
}
