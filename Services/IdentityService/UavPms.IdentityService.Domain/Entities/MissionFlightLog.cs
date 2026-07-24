using System;
using UavPms.IdentityService.Domain.Common;

namespace UavPms.IdentityService.Domain.Entities;

public class MissionFlightLog : BaseEntity
{
    public Guid MissionId { get; set; }
    public string GpsTrack { get; set; } = string.Empty; // Will be mapped to jsonb
    public double MinBatteryRecorded { get; set; }
    public double MaxAltitudeM { get; set; }
    public int FlightDurationSeconds { get; set; }
    public string ConnectionStatus { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public virtual Mission? Mission { get; set; }
}
