using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Missions.Commands.CreateMission;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Application.Features.Missions.Commands.DeleteMission;
using UavPms.OperationsService.Application.Features.Missions.Commands.UpdateMission;
using UavPms.OperationsService.Application.Features.Missions.Queries.GetMissionDetails;
using UavPms.OperationsService.Application.Features.Missions.Queries.GetMyMissions;
using UavPms.OperationsService.Application.Features.Missions.Queries.ListMissions;
using UavPms.Shared.Contracts.Constants;

using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/missions")]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Authorize]
public class MissionController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IMissionLifecycleService? _lifecycle;

    public MissionController(ISender mediator, IMissionLifecycleService? lifecycle = null)
    {
        _mediator = mediator;
        _lifecycle = lifecycle;
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    [Obsolete("Direct mission creation via POST /api/v1/missions is deprecated. All missions must be created via Pre-Mission Assessment workflow (POST /api/v2/pre-mission-assessments/{id}/create-mission).")]
    public async Task<IActionResult> Create([FromBody] CreateMissionRequest request, CancellationToken cancellationToken = default)
    {
        Response?.Headers?.TryAdd("Warning", "299 - \"Direct mission creation is deprecated. All missions must be created via Pre-Mission Assessment workflow (POST /api/v2/pre-mission-assessments/{id}/create-mission).\"");
        if (request.RegionId.HasValue)
        {
            var assessmentId = request.AssessmentId ?? request.SourceAssessmentId;
            var isExplicitAdHoc = string.Equals(request.MissionType, "AD_HOC", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(request.MissionType, "AdHoc", StringComparison.OrdinalIgnoreCase);

            var missionType = (isExplicitAdHoc || request.ScheduleId is null || assessmentId is not null)
                ? MissionType.AdHoc
                : MissionType.Scheduled;

            if (!request.PlannedStart.HasValue || !request.PlannedEnd.HasValue)
                return BadRequest(new ApiResponse(false, "PlannedStart and PlannedEnd are required"));

            var mission = await _lifecycle!.CreateAsync(new Mf01CreateMission(
                request.Title ?? request.Name ?? "",
                request.RegionId.Value,
                missionType,
                request.ScheduleId,
                request.TriggerReason,
                ToUtc(request.PlannedStart.Value),
                ToUtc(request.PlannedEnd.Value),
                request.Description,
                request.ConfirmationDeadline.HasValue ? ToUtc(request.ConfirmationDeadline.Value) : null,
                request.ManagerInstructions,
                request.AssignedToUserId ?? request.InspectorId,
                request.DroneId ?? request.UavId,
                request.Assignments,
                assessmentId,
                request.Priority,
                request.Objective,
                request.PriorityDefects,
                request.EmergencyReason,
                request.IsImmediate), cancellationToken);
            return Ok(new ApiResponse(true, "Mission created successfully", mission.Id));
        }
        var command = new CreateMissionCommand(
            request.Title ?? request.Name ?? string.Empty,
            request.RouteData,
            request.AssignedToUserId ?? request.InspectorId ?? Guid.Empty,
            request.DroneCode,
            request.Status,
            request.Description,
            request.ScheduledStartAt ?? request.ScheduledAt,
            request.InspectorId,
            request.UavId ?? request.DroneId,
            request.TargetAssetIds,
            request.Priority,
            request.Objective,
            request.PriorityDefects,
            request.EmergencyReason,
            request.IsImmediate);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new ApiResponse(true, "Mission created successfully", result));
    }

    [HttpPost("{id:guid}/scope/resolve")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> ResolveScope(Guid id, [FromBody] MissionScopeRequest request, CancellationToken ct)
    {
        var assets = await _lifecycle!.ResolveScopeAsync(id, request.BoundaryWkt, ct);
        return Ok(new ApiResponse(true, "Mission scope resolved", assets.Select(MapScopeAsset).ToList()));
    }

    [HttpPut("{id:guid}/assets")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> ConfirmAssets(Guid id, [FromBody] MissionAssetsRequest request, CancellationToken ct) { await _lifecycle!.ConfirmAssetsAsync(id, request.BoundaryWkt, request.AssetIds, ct); return Ok(new ApiResponse(true, "Mission assets confirmed")); }

    [HttpGet("{id:guid}/assignments")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> GetAssignments(Guid id, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var result = await _lifecycle.GetAssignmentsOverviewAsync(id, ct);
        return Ok(new ApiResponse(true, "Mission assignments overview retrieved successfully", result));
    }

    [HttpPost("{id:guid}/assignments")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] MissionAssignmentRequest request, CancellationToken ct)
    {
        var assignment = await _lifecycle!.AssignAsync(id, new Mf01Assignment(request.UserId, request.AssignmentRole), ct);
        return Ok(new ApiResponse(true, "Mission assignment added", MapAssignment(assignment)));
    }

    [HttpDelete("{id:guid}/assignments/{assignmentId:guid}")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> RemoveAssignment(Guid id, Guid assignmentId, CancellationToken ct) { await _lifecycle!.RemoveAssignmentAsync(id, assignmentId, ct); return Ok(new ApiResponse(true, "Mission assignment removed")); }

    [HttpPut("{id:guid}/drone")]
    [HttpPost("{id:guid}/drone")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> AssignDrone(Guid id, [FromBody] MissionDroneRequest request, CancellationToken ct) { await _lifecycle!.AssignDroneAsync(id, request.DroneId, ct); return Ok(new ApiResponse(true, "Mission drone assigned")); }

    [HttpPost("{id:guid}/drone-handover")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> Handover(Guid id, [FromBody] MissionHandoverRequest request, CancellationToken ct)
    {
        var handover = await _lifecycle!.ConfirmHandoverAsync(id, new Mf01Handover(request.DroneId, request.ReceivedBy, request.Condition, request.Accepted), ct);
        return Ok(new ApiResponse(true, "Drone handover confirmed", MapHandover(handover)));
    }

    [HttpPost("{id:guid}/drone-handover/return")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> ReturnHandover(Guid id, [FromBody] ReturnDroneHandoverRequest request, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var handover = await _lifecycle.ReturnDroneHandoverAsync(id, request.DroneId, request.Condition, ct);
        return Ok(new ApiResponse(true, "Drone return handover confirmed", MapHandover(handover)));
    }

    [HttpPost("{id:guid}/check-in")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> CheckIn(Guid id, [FromBody] MissionCheckInRequest? request = null, CancellationToken ct = default)
    {
        if (request?.Latitude.HasValue == true && (request.Latitude < -90 || request.Latitude > 90))
            return BadRequest(new ApiResponse(false, "Latitude must be between -90 and 90."));

        if (request?.Longitude.HasValue == true && (request.Longitude < -180 || request.Longitude > 180))
            return BadRequest(new ApiResponse(false, "Longitude must be between -180 and 180."));

        var checkIn = await _lifecycle!.CheckInAsync(id, ct);
        return Ok(new ApiResponse(true, "Checked in", MapCheckIn(checkIn)));
    }

    [HttpPost("{id:guid}/assignments/accept")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> AcceptAssignment(Guid id, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var assignment = await _lifecycle.AcceptAssignmentAsync(id, ct);
        return Ok(new ApiResponse(true, "Assignment accepted", MapAssignment(assignment)));
    }

    [HttpPost("{id:guid}/assignments/postpone")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> PostponeAssignment(Guid id, [FromBody] PostponeAssignmentRequest request, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var assignment = await _lifecycle.PostponeAssignmentAsync(id, request.Reason, ct);
        return Ok(new ApiResponse(true, "Assignment postponed", MapAssignment(assignment)));
    }

    [HttpPost("{id:guid}/start")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct) { await _lifecycle!.StartAsync(id, ct); return Ok(new ApiResponse(true, "Mission started")); }
    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct) { await _lifecycle!.CompleteAsync(id, ct); return Ok(new ApiResponse(true, "Mission completed")); }

    [HttpPost("{id:guid}/flight-log")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> UploadFlightLog(Guid id, [FromBody] UploadFlightLogRequest request, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var log = await _lifecycle.UploadFlightLogAsync(id, request, ct);
        return Ok(new ApiResponse(true, "Flight log uploaded successfully", MapFlightLog(log)));
    }

    [HttpGet("{id:guid}/flight-logs")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> GetFlightLogs(Guid id, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var logs = await _lifecycle.GetFlightLogsAsync(id, ct);
        return Ok(new ApiResponse(true, "Flight logs retrieved successfully", logs.Select(MapFlightLog)));
    }

    [HttpPost("{id:guid}/incidents")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> SubmitIncident(Guid id, [FromBody] SubmitIncidentReportRequest request, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var incident = await _lifecycle.SubmitIncidentReportAsync(id, request, ct);
        return Ok(new ApiResponse(true, "Incident report submitted successfully", MapIncidentReport(incident)));
    }

    [HttpGet("{id:guid}/incidents")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> GetIncidents(Guid id, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var incidents = await _lifecycle.GetIncidentReportsAsync(id, ct);
        return Ok(new ApiResponse(true, "Incident reports retrieved successfully", incidents.Select(MapIncidentReport)));
    }

    [HttpPost("{id:guid}/media")]
    [Authorize(Roles = UserRoles.InspectorOnly)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMedia(
        Guid id,
        [FromForm] Guid assetId,
        [FromForm] DateTime capturedAt,
        [FromForm] double? latitude,
        [FromForm] double? longitude,
        IFormFile file,
        CancellationToken ct = default)
    {
        await using var stream = file?.OpenReadStream() ?? Stream.Null;

        var command = new UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage.UploadInspectionImageCommand
        {
            MissionId = id,
            AssetId = assetId,
            CapturedAt = capturedAt,
            Latitude = latitude,
            Longitude = longitude,
            FileStream = stream,
            FileName = file?.FileName ?? string.Empty,
            ContentType = file?.ContentType ?? string.Empty,
        };

        var result = await _mediator.Send(command, ct);
        return Ok(new ApiResponse(true, "Media uploaded successfully.", result));
    }
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelMissionRequest? request = null, CancellationToken ct = default)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var mission = await _lifecycle.CancelMissionAsync(id, request?.Reason, ct);
        return Ok(new ApiResponse(true, "Mission cancelled", MapMissionResult(mission)));
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] ConfirmMissionRequest? request = null, CancellationToken ct = default)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var mission = await _lifecycle.ConfirmMissionAsync(id, request?.Reason, ct);
        return Ok(new ApiResponse(true, "Mission confirmed successfully", MapMissionResult(mission)));
    }

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] SuspendMissionRequest request, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var mission = await _lifecycle.SuspendMissionAsync(id, request.Reason, ct);
        return Ok(new ApiResponse(true, "Mission suspended successfully", MapMissionResult(mission)));
    }

    [HttpPost("{id:guid}/resume")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Resume(Guid id, [FromBody] ResumeMissionRequest? request = null, CancellationToken ct = default)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var mission = await _lifecycle.ResumeMissionAsync(id, request?.Reason, ct);
        return Ok(new ApiResponse(true, "Mission resumed successfully", MapMissionResult(mission)));
    }

    [HttpPost("{id:guid}/postpone")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> Postpone(Guid id, [FromBody] PostponeAssignmentRequest request, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var mission = await _lifecycle.PostponeMissionAsync(id, request.Reason, ct);
        return Ok(new ApiResponse(true, "Mission postponed successfully", MapMissionResult(mission)));
    }

    [HttpPost("{id:guid}/remind")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Remind(Guid id, [FromBody] RemindMissionRequest? request = null, CancellationToken ct = default)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        await _lifecycle.RemindMissionAsync(id, request?.Reason, ct);
        return Ok(new ApiResponse(true, "Mission reminder sent successfully"));
    }

    [HttpPost("{id:guid}/communications")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> AddCommunication(Guid id, [FromBody] SendCommunicationRequest request, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var log = await _lifecycle.AddCommunicationAsync(id, request.Message, ct);
        return Ok(new ApiResponse(true, "Communication message recorded", log));
    }

    [HttpGet("{id:guid}/communications")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> GetCommunications(Guid id, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var logs = await _lifecycle.GetCommunicationsAsync(id, ct);
        return Ok(new ApiResponse(true, "Communications retrieved successfully", logs));
    }

    [HttpGet("{id:guid}/activities")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> GetActivities(Guid id, CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var logs = await _lifecycle.GetActivitiesAsync(id, ct);
        return Ok(new ApiResponse(true, "Activities retrieved successfully", logs));
    }

    [HttpPost("{id:guid}/activities")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> AddActivity(
        Guid id,
        [FromBody] CreateMissionActivityRequest request,
        CancellationToken ct)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var activity = await _lifecycle.AddActivityAsync(id, request, ct);
        return Ok(new ApiResponse(true, "Activity recorded successfully", activity));
    }

    [HttpGet("{id:guid}/detections")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> GetDetections(
        Guid id,
        [FromQuery] string? status = null,
        [FromQuery] string? mediaType = null,
        [FromQuery] bool? isEmergency = null,
        CancellationToken ct = default)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var detections = await _lifecycle.GetMissionDetectionsAsync(id, status, mediaType, isEmergency, ct);
        return Ok(new ApiResponse(true, "Mission detections retrieved successfully", detections));
    }

    [HttpPost("{missionId:guid}/detections/{detectionId:guid}/review")]
    [HttpPut("{missionId:guid}/detections/{detectionId:guid}/review")]
    [Authorize(Roles = UserRoles.AdminManagerAnalyst)]
    public async Task<IActionResult> ReviewDetection(
        Guid missionId,
        Guid detectionId,
        [FromBody] ReviewDetectionRequest request,
        CancellationToken ct = default)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var result = await _lifecycle.ReviewDetectionAsync(missionId, detectionId, request, ct);
        return Ok(new ApiResponse(true, "AI detection review saved successfully", result));
    }

    [HttpGet("{id:guid}/maintenance-tasks")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> GetMaintenanceTasks(Guid id, CancellationToken ct = default)
    {
        if (_lifecycle == null) return BadRequest(new ApiResponse(false, "Lifecycle service unavailable"));
        var tasks = await _lifecycle.GetMissionMaintenanceTasksAsync(id, ct);
        return Ok(new ApiResponse(true, "Maintenance tasks retrieved successfully", tasks));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMissionRequest request, CancellationToken cancellationToken = default)
    {
        var command = new UpdateMissionCommand(
            id,
            request.Title,
            request.RouteData,
            request.AssignedToUserId,
            request.DroneCode,
            request.Status,
            request.Description);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new ApiResponse(true, "Mission updated successfully", result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        await _mediator.Send(new DeleteMissionCommand(id), cancellationToken);
        return Ok(new ApiResponse(true, "Mission deleted successfully"));
    }

    [HttpGet]
    [Authorize(Roles = UserRoles.AdminManagerAnalyst)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] string? sortBy = "createdAt",
        [FromQuery] bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0 || pageSize <= 0)
        {
            return BadRequest(new ApiResponse(false, "Invalid page or page size"));
        }

        if (pageSize > 100)
        {
            return BadRequest(new ApiResponse(false, "Invalid page or page size"));
        }
        
        var query = new ListMissionsQuery(page, pageSize, search, status, sortBy, sortDescending, priority);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(new ApiResponse(true, "Mission list retrieved successfully", result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMissionDetailsQuery(id), cancellationToken);
        return Ok(new ApiResponse(true, "Mission details retrieved successfully", result));
    }

    [HttpGet("my")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> GetMyMissions(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyMissionsQuery(), cancellationToken);
        return Ok(new ApiResponse(true, "Missions retrieved successfully", result));
    }

    private static DateTime ToUtc(DateTime dt) =>
        dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };

    private static MissionOperationResultDto MapMissionResult(Mission m) => new()
    {
        Id = m.Id,
        MissionCode = m.MissionCode,
        Title = m.Title,
        Status = m.Status.ToString(),
        Priority = m.Priority.ToString(),
        RegionId = m.RegionId,
        InspectorId = m.InspectorId,
        UavId = m.UavId,
        ManagerId = m.ManagerId,
        PlannedStart = m.PlannedStart,
        PlannedEnd = m.PlannedEnd,
        ConfirmationDeadline = m.ConfirmationDeadline,
        AcceptedAt = m.AcceptedAt,
        StartedAt = m.StartedAt,
        EndedAt = m.EndedAt,
        PostponedAt = m.PostponedAt,
        ManagerInstructions = m.ManagerInstructions,
        Version = m.Version,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };

    private static MissionAssignmentResponseDto MapAssignment(MissionAssignment a) => new()
    {
        Id = a.Id,
        MissionId = a.MissionId,
        UserId = a.UserId,
        AssignmentRole = a.AssignmentRole,
        Status = a.Status.ToString(),
        ResponseStatus = a.ResponseStatus.ToString(),
        IsRequired = a.IsRequired,
        AssignedAt = a.AssignedAt,
        RespondedAt = a.RespondedAt,
        ResponseReason = a.ResponseReason,
        Version = a.Version,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt
    };

    private static DroneHandoverResponseDto MapHandover(DroneHandover h) => new()
    {
        Id = h.Id,
        MissionId = h.MissionId,
        DroneId = h.DroneId,
        HandedOverBy = h.HandedOverBy,
        ReceivedBy = h.ReceivedBy,
        Condition = h.Condition,
        Status = h.Status.ToString(),
        ReceivedAt = h.ReceivedAt,
        ReturnedAt = h.ReturnedAt,
        CreatedAt = h.CreatedAt
    };

    private static MissionCheckInResponseDto MapCheckIn(MissionCheckIn c) => new()
    {
        Id = c.Id,
        MissionId = c.MissionId,
        UserId = c.UserId,
        CheckedInAt = c.CheckedInAt,
        Status = c.Status.ToString(),
        CreatedAt = c.CreatedAt
    };

    private static MissionScopeAssetDto MapScopeAsset(Asset a) => new()
    {
        Id = a.Id,
        AssetCode = a.AssetCode,
        AssetType = a.AssetType,
        TowerId = a.TowerId,
        PowerLineId = a.PowerLineId,
        ManagementUnitId = a.ManagementUnitId,
        Latitude = a.Location?.Y,
        Longitude = a.Location?.X
    };

    private static MissionFlightLogDto MapFlightLog(MissionFlightLog l) => new()
    {
        Id = l.Id,
        MissionId = l.MissionId,
        GpsTrack = l.GpsTrack,
        MinBatteryRecorded = l.MinBatteryRecorded,
        MaxAltitudeM = l.MaxAltitudeM,
        FlightDurationSeconds = l.FlightDurationSeconds,
        ConnectionStatus = l.ConnectionStatus,
        RecordedAt = l.RecordedAt
    };

    private static IncidentReportDto MapIncidentReport(IncidentReport r) => new()
    {
        Id = r.Id,
        MissionId = r.MissionId,
        ReportedBy = r.ReportedBy,
        ReporterName = r.Reporter?.FullName ?? string.Empty,
        AssetId = r.AssetId,
        IncidentType = r.IncidentType,
        Severity = r.Severity,
        Description = r.Description,
        FileUrl = r.FileUrl,
        Status = r.Status,
        ReportedAt = r.ReportedAt
    };
}

public record CreateMissionRequest(
    string? Title,
    string? Name,
    string? RouteData,
    Guid? AssignedToUserId,
    string? DroneCode,
    string? Status,
    string? Description,
    DateTime? ScheduledStartAt,
    DateTime? ScheduledAt,
    Guid? InspectorId,
    Guid? UavId,
    Guid? DroneId,
    IReadOnlyList<Guid>? TargetAssetIds,
    Guid? RegionId = null,
    string? MissionType = null,
    Guid? ScheduleId = null,
    string? TriggerReason = null,
    DateTime? PlannedStart = null,
    DateTime? PlannedEnd = null,
    DateTime? ConfirmationDeadline = null,
    string? ManagerInstructions = null,
    IReadOnlyList<MissionAssignmentItemRequest>? Assignments = null,
    Guid? AssessmentId = null,
    Guid? SourceAssessmentId = null,
    MissionPriority Priority = MissionPriority.Normal,
    InspectionObjective Objective = InspectionObjective.PeriodicInspection,
    IReadOnlyList<string>? PriorityDefects = null,
    string? EmergencyReason = null,
    bool IsImmediate = false);

public record MissionScopeRequest(string BoundaryWkt);
public record MissionAssetsRequest(string BoundaryWkt, IReadOnlyCollection<Guid> AssetIds);
public record MissionAssignmentRequest(Guid UserId, string AssignmentRole);
public record MissionDroneRequest(Guid DroneId);
public record MissionHandoverRequest(Guid DroneId, Guid ReceivedBy, string Condition, bool Accepted);

public record ConfirmMissionRequest(string? Reason = null);
public record SuspendMissionRequest(string Reason);
public record ResumeMissionRequest(string? Reason = null);
public record CancelMissionRequest(string? Reason = null);
public record RemindMissionRequest(string? Reason = null);
public record SendCommunicationRequest(string Message);

public record UpdateMissionRequest(
    string Title,
    string RouteData,
    Guid AssignedToUserId,
    string DroneCode,
    string Status,
    string? Description);
