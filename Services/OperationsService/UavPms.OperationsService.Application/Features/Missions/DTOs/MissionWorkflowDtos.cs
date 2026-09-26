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
