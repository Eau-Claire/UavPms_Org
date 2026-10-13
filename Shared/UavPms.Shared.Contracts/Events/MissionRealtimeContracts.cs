using System;
using System.Text.Json.Serialization;

namespace UavPms.Shared.Contracts.Events;

public class MissionLifecycleEventDto
{
    public string MissionId { get; set; } = string.Empty;

    // "DISPATCHED" | "CONFIRMED" | "POSTPONED" | "SUSPENDED" | "RESUMED" | "CANCELLED" | "REMINDER" | "COMMUNICATION" | "OVERDUE"
    public string Type { get; set; } = string.Empty;

    // "PENDING_CONFIRMATION" | "CONFIRMED" | "POSTPONED" | "SUSPENDED" | "Cancelled"
    public string? Status { get; set; }

    public string? ConfirmationDeadline { get; set; }
    public string? ManagerInstructions { get; set; }
    public string? Reason { get; set; }
    public string? Message { get; set; }

    public string? ActorId { get; set; }
    public string? ActorName { get; set; }

    // "MANAGER" | "INSPECTOR" | "SYSTEM"
    public string? ActorRole { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public MissionCommunicationLogDto? Log { get; set; }

    // Target users for push routing
    public string? TargetUserId { get; set; }
    public string? InspectorId { get; set; }
    public string? ManagerId { get; set; }
    public string? MissionCode { get; set; }
    public string? MissionTitle { get; set; }
    public System.Collections.Generic.List<string>? AssignedUserIds { get; set; }
}

public class MissionCommunicationLogDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = "SYSTEM"; // "MANAGER" | "INSPECTOR" | "SYSTEM"
    public string Type { get; set; } = "MESSAGE";       // "MESSAGE" | "DISPATCH" | "CONFIRM" | "POSTPONE" | "SUSPEND" | "RESUME" | "CANCEL" | "REMINDER" | "OVERDUE"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class MissionLifecycleRealtimeEvent
{
    public MissionLifecycleEventDto Event { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AssessmentRealtimeEventDto
{
    public string AssessmentId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? DroneId { get; set; }
    public string? DroneCode { get; set; }
    public string? TechnicalHealth { get; set; }
    public string? TechnicalStatus { get; set; }
    public string? ConsumedMissionId { get; set; }
    public string? ConsumedMissionCode { get; set; }
    public string? CompletedMissionId { get => ConsumedMissionId; set => ConsumedMissionId = value; }
    public string? CompletedMissionCode { get => ConsumedMissionCode; set => ConsumedMissionCode = value; }
    public double? WindSpeed { get; set; }
    public double? WindGust { get; set; }
    public bool? IsMeteoSafe { get; set; }
    public string? Message { get; set; }
    public string? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? ActorRole { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class AssessmentRealtimeIntegrationEvent
{
    public AssessmentRealtimeEventDto Event { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
