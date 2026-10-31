using Microsoft.EntityFrameworkCore;

namespace UavPms.OperationsService.Infrastructure.Persistence.Configurations;

/// <summary>
/// varchar lengths from the ERD v2.2 (plan item 2.7) for columns whose values the server controls:
/// enum/status strings, generated codes, token hashes and storage URLs.
/// Columns filled from free user or client input (names, emails, tower/UAV codes, incident types,
/// material logs, Idempotency-Key, User-Agent, IP, notification titles) stay <c>text</c> until the API
/// validates their length; narrowing them now would turn a long but valid request into a 500.
/// </summary>
public static class ErdColumnLengths
{
    public static readonly (string Table, string Column, int Length)[] Columns =
    [
        ("Users", "Status", 20),
        ("Roles", "RoleName", 50),
        ("RefreshTokens", "TokenHash", 128),
        ("TrustedDevices", "DeviceTokenHash", 128),
        ("UAVs", "FlightStatus", 20),
        ("UAVs", "OperationalStatus", 20),
        ("UAVs", "TechnicalHealth", 20),
        ("DroneTechnicalInspections", "Status", 20),
        ("DroneTechnicalInspections", "Health", 20),
        ("DroneTechnicalInspections", "SourceType", 20),
        ("DroneTechnicalInspections", "PolicyVersion", 50),
        ("DroneTechnicalMetrics", "Severity", 20),
        ("PreMissionAssessments", "Status", 20),
        ("PreMissionAssessments", "SiteFeasibilityStatus", 20),
        ("PreMissionAssessments", "OverallTechnicalHealth", 20),
        ("PreMissionAssessments", "EvaluationPolicyVersion", 50),
        ("PreMissionAssessmentPersonnel", "AvailabilityStatus", 20),
        ("PreMissionAssessmentPersonnel", "EligibilityStatus", 20),
        ("PreMissionAssessmentPersonnel", "ReasonCode", 50),
        ("PreMissionAssessmentDrones", "OperationalAvailabilityStatus", 20),
        ("PreMissionAssessmentDrones", "TechnicalEligibilityStatus", 20),
        ("PreMissionAssessmentDrones", "TechnicalHealth", 20),
        ("PreMissionAssessmentDrones", "Suitability", 20),
        ("PreMissionAssessmentDrones", "ReasonCode", 50),
        ("Missions", "MissionCode", 50),
        ("Missions", "Objective", 30),
        ("Missions", "MissionType", 20),
        ("Missions", "Status", 30),
        ("MissionTargets", "InspectionStatus", 20),
        ("MissionAssignments", "Status", 20),
        ("MissionAssignments", "ResponseStatus", 20),
        ("ResourceBookings", "Status", 20),
        ("MissionCheckIns", "Status", 20),
        ("DroneHandovers", "Status", 20),
        ("MissionFlightLogs", "ConnectionStatus", 20),
        ("IncidentReports", "Status", 20),
        ("InspectionMedia", "MediaType", 10),
        ("InspectionMedia", "FileUrl", 2048),
        ("InspectionMedia", "ValidationStatus", 20),
        ("EmergencyAlerts", "Status", 20),
        ("MaintenanceTickets", "TicketCode", 50),
        ("MaintenanceTickets", "Status", 30),
        ("MaintenanceTickets", "Priority", 20),
        ("MaintenanceProofs", "FileUrl", 2048),
        ("Notifications", "Type", 50),
        ("Notifications", "ReferenceType", 50),
        ("AuditLogs", "TableName", 100),
        ("AuditLogs", "ActionType", 100),
    ];

    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var (table, column, length) in Columns)
        {
            var entity = modelBuilder.Model.GetEntityTypes().SingleOrDefault(e => e.GetTableName() == table)
                ?? throw new InvalidOperationException($"ERD column length: table '{table}' is not mapped.");
            var property = entity.GetProperties().SingleOrDefault(p => p.GetColumnName() == column)
                ?? throw new InvalidOperationException($"ERD column length: column '{table}.{column}' is not mapped.");
            property.SetMaxLength(length);
        }
    }
}
