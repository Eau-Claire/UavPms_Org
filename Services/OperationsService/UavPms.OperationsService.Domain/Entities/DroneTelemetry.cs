using System;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class DroneTelemetry : BaseEntity
{
    public Guid UavId { get; set; }
    public Guid? MissionId { get; set; }
    public DateTime SourceTimestamp { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string? FlightMode { get; set; }
    public bool? Armed { get; set; }
    public Point? Location { get; set; }
    public double? AltRelativeM { get; set; }
    public double? AltMslM { get; set; }
    public int? Satellites { get; set; }
    public int? GpsFixType { get; set; }
    public double? Hdop { get; set; }
    public double? RollDeg { get; set; }
    public double? PitchDeg { get; set; }
    public double? YawDeg { get; set; }
    public double? GroundSpeedMs { get; set; }
    public double? AirSpeedMs { get; set; }
    public double? ClimbRateMs { get; set; }
    public double? HeadingDeg { get; set; }
    public double? ThrottlePct { get; set; }
    public double? BatteryVoltageV { get; set; }
    public double? BatteryCurrentA { get; set; }
    public int? BatteryRemainingPct { get; set; }

    public virtual Uav? Uav { get; set; }
    public virtual Mission? Mission { get; set; }
}
