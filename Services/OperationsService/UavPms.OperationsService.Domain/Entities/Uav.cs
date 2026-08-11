using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

public class Uav : BaseEntity
{
    public string UavCode { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DroneStatus Status { get; set; } = DroneStatus.Idle;
    public double BatteryLevel { get; set; }
    public Point? CurrentLocation { get; set; }
    public DateTime? LastMaintenanceAt { get; set; }

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();
    
    #region Rich Domain Methods

    public void UpdateStatus(DroneStatus status)
    {
        Status = status;
    }

    public void UpdateBatteryLevel(double batteryLevel)
    {
        if(batteryLevel < 0 || batteryLevel > 100)
            throw new ArgumentOutOfRangeException(nameof(batteryLevel), "Battery level must be between 0 and 100.");
        
        BatteryLevel = batteryLevel;
    }
    
    public void UpdateCurrentLocation(Point location)
    {
        CurrentLocation = location ?? throw new ArgumentNullException(nameof(location));
    }
    #endregion
}
