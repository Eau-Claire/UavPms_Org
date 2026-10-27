using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

/// <summary>
/// Cột điện: đơn vị hạ tầng nhỏ nhất được quản lý, thuộc trực tiếp một khu vực.
/// </summary>
public class Tower : BaseEntity
{
    public Guid RegionId { get; set; }
    public string TowerCode { get; set; } = string.Empty;
    public string? PoleType { get; set; }
    public string? Address { get; set; }
    public Geometry? Geom { get; set; }

    public virtual Region? Region { get; set; }
    public virtual ICollection<MissionTarget> MissionTargets { get; set; } = new List<MissionTarget>();
    public virtual ICollection<InspectionMedia> InspectionMedia { get; set; } = new List<InspectionMedia>();
    public virtual ICollection<DetectedAnomaly> DetectedAnomalies { get; set; } = new List<DetectedAnomaly>();
    public virtual ICollection<IncidentReport> IncidentReports { get; set; } = new List<IncidentReport>();
}
