using System;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class DroneFlightController : BaseEntity
{
    public Guid DroneId { get; set; }
    public string FlightControllerUid { get; set; } = string.Empty;
    public string? BoardIdentifier { get; set; }
    public string? FirmwareName { get; set; } = "Betaflight";
    public string? FirmwareVersion { get; set; }
    public DateTime PairedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnpairedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual Uav Drone { get; set; } = null!;
}
