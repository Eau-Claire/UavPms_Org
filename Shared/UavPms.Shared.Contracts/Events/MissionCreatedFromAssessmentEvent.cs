using System;

namespace UavPms.Shared.Contracts.Events;

public class MissionCreatedFromAssessmentEvent
{
    public Guid MissionId { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid ManagerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedEnd { get; set; }
    public int PersonnelCount { get; set; }
    public int DroneCount { get; set; }
}
