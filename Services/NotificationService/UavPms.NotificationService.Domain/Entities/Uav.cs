using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using UavPms.NotificationService.Domain.Common;

namespace UavPms.NotificationService.Domain.Entities;

public class Uav : BaseEntity
{
    public string UavCode { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double BatteryLevel { get; set; }
    public Point? CurrentLocation { get; set; }
    public DateTime? LastMaintenanceAt { get; set; }

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();
}
