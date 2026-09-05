namespace UavPms.OperationsService.Domain.Enums;

public enum DroneStatus
{
    Idle = 0,
    Flying = 1,
    Maintenance = 2,
    Offline = 3,
    // Kept for compatibility with existing database rows and integrations.
    Online = 4,
}
