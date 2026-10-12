using System;
using System.Collections.Generic;

namespace UavPms.OperationsService.Application.Features.Missions.DTOs;

public class MissionDetectionBoundingBoxDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public class MissionDetectionDto
{
    public string Id { get; set; } = string.Empty;
    public string MissionId { get; set; } = string.Empty;
    public string MediaId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public double SeverityWeight { get; set; }
    public bool IsEmergency { get; set; }
    public string Status { get; set; } = "Pending"; // "Pending" | "Approved" | "Rejected"
    public MissionDetectionBoundingBoxDto? BoundingBox { get; set; }
    public double? TimestampSeconds { get; set; }
    public string? TimestampLabel { get; set; }
    public int? FrameIndex { get; set; }
    public string? ImageUrl { get; set; }
    public string? SourceUrl { get; set; }
    public string? AssetId { get; set; }
    public string? Tower { get; set; }
    public string? Gps { get; set; }
    public string? Description { get; set; }
    public DateTime? DetectedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}

public record ReviewDetectionRequest(
    string Status, // "Approved" | "Rejected"
    string? ReviewNotes = null,
    string? OverrideSeverity = null);

public class ReviewDetectionResultDto
{
    public string DetectionId { get; set; } = string.Empty;
    public string MissionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ReviewNotes { get; set; }
    public string? MaintenanceTaskId { get; set; }
    public double? NewAssetHealthScore { get; set; }
    public DateTime ReviewedAt { get; set; }
}

public class MissionMaintenanceTaskDto
{
    public string Id { get; set; } = string.Empty;
    public string MissionId { get; set; } = string.Empty;
    public string DetectionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium"; // "Urgent" | "High" | "Medium" | "Low"
    public string TowerCode { get; set; } = string.Empty;
    public string AssetCode { get; set; } = string.Empty;
    public string DefectDescription { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // "Pending" | "Approved" | "InProgress" | "Completed"
    public string AssignedTeam { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MissionActivityDto
{
    public string Id { get; set; } = string.Empty;
    public string MissionId { get; set; } = string.Empty;
    public string SenderUserId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = "SYSTEM"; // "MANAGER" | "INSPECTOR" | "ANALYST" | "TECHNICIAN" | "SYSTEM"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public record CreateMissionActivityRequest(
    string? Content,
    string? Message = null,
    string? SenderRole = null);

public class MissionAssignmentItemDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public string AssignmentRole { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string ResponseStatus { get; set; } = "Pending"; // "Pending" | "Accepted" | "Postponed" | "Replaced"
    public bool IsRequired { get; set; } = true;
    public DateTime AssignedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? ResponseReason { get; set; }
}

public class MissionAssignmentsOverviewDto
{
    public string MissionId { get; set; } = string.Empty;
    public int TotalRequiredCount { get; set; }
    public int ConfirmedCount { get; set; }
    public bool AllConfirmed { get; set; }
    public DateTime? ConfirmationDeadline { get; set; }
    public List<MissionAssignmentItemDto> Assignments { get; set; } = new();
}

public class MissionOperationResultDto
{
    public Guid Id { get; set; }
    public string MissionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public Guid? RegionId { get; set; }
    public Guid? InspectorId { get; set; }
    public Guid? UavId { get; set; }
    public Guid? ManagerId { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? ConfirmationDeadline { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? PostponedAt { get; set; }
    public string? ManagerInstructions { get; set; }
    public uint Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MissionAssignmentResponseDto
{
    public Guid Id { get; set; }
    public Guid MissionId { get; set; }
    public Guid UserId { get; set; }
    public string AssignmentRole { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ResponseStatus { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? ResponseReason { get; set; }
    public uint Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class DroneHandoverResponseDto
{
    public Guid Id { get; set; }
    public Guid MissionId { get; set; }
    public Guid DroneId { get; set; }
    public Guid HandedOverBy { get; set; }
    public Guid ReceivedBy { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MissionCheckInResponseDto
{
    public Guid Id { get; set; }
    public Guid MissionId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CheckedInAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MissionScopeAssetDto
{
    public Guid Id { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public Guid TowerId { get; set; }
    public Guid? PowerLineId { get; set; }
    public Guid? ManagementUnitId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class MissionFlightLogDto
{
    public Guid Id { get; set; }
    public Guid MissionId { get; set; }
    public string GpsTrack { get; set; } = string.Empty;
    public double MinBatteryRecorded { get; set; }
    public double MaxAltitudeM { get; set; }
    public int FlightDurationSeconds { get; set; }
    public string ConnectionStatus { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}

public record UploadFlightLogRequest(
    string GpsTrack,
    double MinBatteryRecorded,
    double MaxAltitudeM,
    int FlightDurationSeconds,
    string ConnectionStatus);

public class IncidentReportDto
{
    public Guid Id { get; set; }
    public Guid MissionId { get; set; }
    public Guid ReportedBy { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReportedAt { get; set; }
}

public record SubmitIncidentReportRequest(
    string IncidentType,
    string Severity,
    string Description,
    Guid? AssetId = null,
    string? FileUrl = null);

public record ReturnDroneHandoverRequest(
    Guid DroneId,
    string Condition);

public record MissionCheckInRequest(
    double? Latitude = null,
    double? Longitude = null,
    string? Notes = null);


