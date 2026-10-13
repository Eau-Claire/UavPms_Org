using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

public class Uav : BaseEntity
{
    public string UavCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Category { get; set; } = "Hexacopter";
    public string SerialNumber { get; set; } = string.Empty;
    public Guid? AssignedRegionId { get; set; }
    public string? AssignedRegionName { get; set; }
    public DroneStatus Status { get; set; } = DroneStatus.Idle;
    public DroneOperationalStatus OperationalStatus { get; set; } = DroneOperationalStatus.Available;
    public TechnicalHealth TechnicalHealth { get; set; } = TechnicalHealth.Unknown;
    public double FlightEnduranceMinutes { get; set; } = 30.0;
    public double OperationalRangeKm { get; set; } = 10.0;
    public string? CameraPayloadInfo { get; set; }
    public Guid? LastTechnicalInspectionId { get; set; }
    public DateTime? TechnicalHealthUpdatedAt { get; set; }
    public double BatteryLevel { get; set; }
    public Point? CurrentLocation { get; set; }
    public DateTime? LastMaintenanceAt { get; set; }
    public uint Version { get; set; } = 1;

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();
    public virtual ICollection<DroneTechnicalInspection> TechnicalInspections { get; set; } = new List<DroneTechnicalInspection>();
    public virtual ICollection<DroneFlightController> FlightControllers { get; set; } = new List<DroneFlightController>();
    
    #region Rich Domain Methods

    public void UpdateStatus(DroneStatus status)
    {
        Status = status;
        Version++;
    }

    public void UpdateBatteryLevel(double batteryLevel)
    {
        if(batteryLevel < 0 || batteryLevel > 100)
            throw new ArgumentOutOfRangeException(nameof(batteryLevel), "Battery level must be between 0 and 100.");
        
        BatteryLevel = batteryLevel;
        Version++;
    }
    
    public void UpdateCurrentLocation(Point location)
    {
        CurrentLocation = location ?? throw new ArgumentNullException(nameof(location));
        Version++;
    }
    #endregion
}
