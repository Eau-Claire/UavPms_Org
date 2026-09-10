namespace UavPms.OperationsService.Domain.Enums;

public enum MissionStatus
{
    Draft = 0,
    Assigned = 1,
    Preparing = 2,
    Ready = 3,
    InProgress = 4,
    Completed = 5,
    Cancelled = 6,

    // Source compatibility for older callers. Persisted legacy values are mapped in EF.
    Pending = Draft,
    Executing = InProgress,
}
