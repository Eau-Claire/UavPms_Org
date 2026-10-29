using System;
using System.Collections.Generic;
using System.Linq;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Application.Features.Reports.DTOs;

public record ReportMissionSummaryDto(
    Guid Id,
    string MissionCode,
    string Title,
    string Status,
    DateTime? PlannedStart
);

/// <summary>
/// Chi tiết báo cáo. Báo cáo không lưu tệp: DefectCount và nội dung PDF/Excel luôn được tính trực tiếp
/// từ phạm vi DateFrom/DateTo/Region/Missions tại thời điểm xem hoặc tải.
/// </summary>
public record ReportDetailDto(
    Guid Id,
    string Code,
    string Title,
    string Type,
    string Status,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    ReportNestedEntityDto? Region,
    ReportCreatorDto? Creator,
    int DefectCount,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    string? RejectionReason,
    ReportCreatorDto? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    List<ReportMissionSummaryDto> Missions,
    bool HasSnapshot = false
);

public static class ReportMapping
{
    public static ReportDetailDto ToDetailDto(this Report report, int defectCount) => new(
        report.Id,
        report.Code,
        report.Title,
        report.Type.ToString().ToLowerInvariant(),
        report.Status.ToString().ToLowerInvariant(),
        report.Description,
        report.CreatedAt,
        report.UpdatedAt,
        report.Region != null ? new ReportNestedEntityDto(report.Region.Id, report.Region.Name) : null,
        report.CreatedByUser != null ? new ReportCreatorDto(report.CreatedByUser.Id, report.CreatedByUser.FullName) : null,
        defectCount,
        report.DateFrom,
        report.DateTo,
        report.RejectionReason,
        report.ApprovedBy != null ? new ReportCreatorDto(report.ApprovedBy.Id, report.ApprovedBy.FullName) : null,
        report.ApprovedAt,
        report.ReportMissions.Select(rm => new ReportMissionSummaryDto(
            rm.MissionId,
            rm.Mission?.MissionCode ?? string.Empty,
            rm.Mission?.Title ?? string.Empty,
            rm.Mission?.Status.ToString().ToLowerInvariant() ?? string.Empty,
            rm.Mission?.PlannedStart
        )).ToList(),
        !string.IsNullOrWhiteSpace(report.Snapshot));

    public static ReportDto ToDto(this Report report, int defectCount) => new(
        report.Id,
        report.Code,
        report.Title,
        report.Type.ToString().ToLowerInvariant(),
        report.Status.ToString().ToLowerInvariant(),
        report.CreatedAt,
        report.Region != null ? new ReportNestedEntityDto(report.Region.Id, report.Region.Name) : null,
        report.CreatedByUser != null ? new ReportCreatorDto(report.CreatedByUser.Id, report.CreatedByUser.FullName) : null,
        defectCount,
        !string.IsNullOrWhiteSpace(report.Snapshot));
}
