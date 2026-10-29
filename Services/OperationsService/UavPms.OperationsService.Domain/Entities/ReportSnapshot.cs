using System;
using System.Collections.Generic;
using System.Linq;

namespace UavPms.OperationsService.Domain.Entities;

public class ReportSnapshot
{
    public Guid ReportId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? RegionId { get; set; }
    public string? RegionName { get; set; }
    public DateTimeOffset? DateFrom { get; set; }
    public DateTimeOffset? DateTo { get; set; }
    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalAnomalies { get; set; }
    public List<ReportSnapshotMission> Missions { get; set; } = new();
    public List<ReportSnapshotAnomaly> Anomalies { get; set; } = new();

    public static ReportSnapshot FromReport(Report report, IReadOnlyList<DetectedAnomaly>? anomalies = null)
    {
        var defectList = anomalies ?? Array.Empty<DetectedAnomaly>();
        return new ReportSnapshot
        {
            ReportId = report.Id,
            Code = report.Code ?? string.Empty,
            Title = report.Title ?? string.Empty,
            Type = report.Type.ToString(),
            Status = report.Status.ToString(),
            Description = report.Description,
            RegionId = report.RegionId,
            RegionName = report.Region?.Name,
            DateFrom = report.DateFrom,
            DateTo = report.DateTo,
            CreatedById = report.CreatedBy,
            CreatedByName = report.CreatedByUser?.FullName,
            ApprovedById = report.ApprovedById,
            ApprovedByName = report.ApprovedBy?.FullName,
            ApprovedAt = report.ApprovedAt,
            CreatedAt = report.CreatedAt,
            TotalAnomalies = defectList.Count,
            Missions = report.ReportMissions?.Select(rm => new ReportSnapshotMission
            {
                MissionId = rm.MissionId,
                MissionCode = rm.Mission?.MissionCode ?? "N/A",
                Title = rm.Mission?.Title ?? "N/A",
                Status = rm.Mission?.Status.ToString() ?? "N/A",
                PlannedStart = rm.Mission?.PlannedStart
            }).ToList() ?? new List<ReportSnapshotMission>(),
            Anomalies = defectList.Select(a => new ReportSnapshotAnomaly
            {
                Id = a.Id,
                CategoryName = a.Category?.CategoryName ?? a.AnalysisRun?.ModelName ?? "Khuyết tật thiết bị",
                MissionCode = a.Media?.Mission?.MissionCode ?? "N/A",
                TowerCode = a.Tower?.TowerCode,
                ConfidenceScore = a.ConfidenceScore,
                ValidationStatus = string.IsNullOrWhiteSpace(a.ValidationStatus) ? "Chờ xác nhận" : a.ValidationStatus,
                CreatedAt = a.CreatedAt,
                AnalystNotes = a.AnalystNotes
            }).ToList()
        };
    }
}

public class ReportSnapshotMission
{
    public Guid MissionId { get; set; }
    public string MissionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PlannedStart { get; set; }
}

public class ReportSnapshotAnomaly
{
    public Guid Id { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string MissionCode { get; set; } = string.Empty;
    public string? TowerCode { get; set; }
    public double ConfidenceScore { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? AnalystNotes { get; set; }
}
