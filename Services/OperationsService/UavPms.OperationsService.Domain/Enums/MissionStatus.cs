namespace UavPms.OperationsService.Domain.Enums;

public enum MissionStatus
{
    Draft = 0,
    PendingAcceptance = 1,
    Confirmed = 2,
    Ready = 3,
    InProgress = 4,
    Suspended = 5,
    Postponed = 6,
    Completed = 7,
    Cancelled = 8,
}
