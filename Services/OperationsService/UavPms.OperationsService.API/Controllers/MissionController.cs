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

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/missions")]
[ApiVersion("1.0")]
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
    public async Task<IActionResult> Create([FromBody] CreateMissionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.RegionId.HasValue)
        {
            if (!Enum.TryParse<MissionType>((request.MissionType ?? "").Replace("_", ""), true, out var missionType))
                return BadRequest(new ApiResponse(false, "MissionType must be SCHEDULED or AD_HOC"));
            if (!request.PlannedStart.HasValue || !request.PlannedEnd.HasValue)
                return BadRequest(new ApiResponse(false, "PlannedStart and PlannedEnd are required"));
            var mission = await _lifecycle!.CreateAsync(new Mf01CreateMission(request.Title ?? request.Name ?? "", request.RegionId.Value,
                missionType, request.ScheduleId, request.TriggerReason, request.PlannedStart.Value, request.PlannedEnd.Value, request.Description), cancellationToken);
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
            request.TargetAssetIds);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new ApiResponse(true, "Mission created successfully", result));
    }

    [HttpPost("{id:guid}/scope/resolve")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> ResolveScope(Guid id, [FromBody] MissionScopeRequest request, CancellationToken ct) =>
        Ok(new ApiResponse(true, "Mission scope resolved", await _lifecycle!.ResolveScopeAsync(id, request.BoundaryWkt, ct)));

    [HttpPut("{id:guid}/assets")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> ConfirmAssets(Guid id, [FromBody] MissionAssetsRequest request, CancellationToken ct) { await _lifecycle!.ConfirmAssetsAsync(id, request.BoundaryWkt, request.AssetIds, ct); return Ok(new ApiResponse(true, "Mission assets confirmed")); }

    [HttpPost("{id:guid}/assignments")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] MissionAssignmentRequest request, CancellationToken ct) => Ok(new ApiResponse(true, "Mission assignment added", await _lifecycle!.AssignAsync(id, new Mf01Assignment(request.UserId, request.AssignmentRole), ct)));

    [HttpDelete("{id:guid}/assignments/{assignmentId:guid}")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> RemoveAssignment(Guid id, Guid assignmentId, CancellationToken ct) { await _lifecycle.RemoveAssignmentAsync(id, assignmentId, ct); return Ok(new ApiResponse(true, "Mission assignment removed")); }

    [HttpPut("{id:guid}/drone")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> AssignDrone(Guid id, [FromBody] MissionDroneRequest request, CancellationToken ct) { await _lifecycle.AssignDroneAsync(id, request.DroneId, ct); return Ok(new ApiResponse(true, "Mission drone assigned")); }

    [HttpPost("{id:guid}/drone-handover")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> Handover(Guid id, [FromBody] MissionHandoverRequest request, CancellationToken ct) => Ok(new ApiResponse(true, "Drone handover confirmed", await _lifecycle.ConfirmHandoverAsync(id, new Mf01Handover(request.DroneId, request.ReceivedBy, request.Condition, request.Accepted), ct)));

    [HttpPost("{id:guid}/check-in")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> CheckIn(Guid id, CancellationToken ct) => Ok(new ApiResponse(true, "Checked in", await _lifecycle.CheckInAsync(id, ct)));

    [HttpPost("{id:guid}/start")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct) { await _lifecycle.StartAsync(id, ct); return Ok(new ApiResponse(true, "Mission started")); }
    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct) { await _lifecycle.CompleteAsync(id, ct); return Ok(new ApiResponse(true, "Mission completed")); }
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await _lifecycle.CancelAsync(id, ct); return Ok(new ApiResponse(true, "Mission cancelled")); }

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
        
        var query = new ListMissionsQuery(page, pageSize, search, status, sortBy, sortDescending);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(new ApiResponse(true, "Mission list retrieved successfully", result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminManagerInspectorAnalyst)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMissionDetailsQuery(id), cancellationToken);
        return Ok(new ApiResponse(true, "Mission details retrieved successfully", result));
    }

    [HttpGet("my")]
    [Authorize(Roles = UserRoles.InspectorOnly)]
    public async Task<IActionResult> GetMyMissions(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyMissionsQuery(), cancellationToken);
        return Ok(new ApiResponse(true, "Missions retrieved successfully", result));
    }
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
    DateTime? PlannedEnd = null);

public record MissionScopeRequest(string BoundaryWkt);
public record MissionAssetsRequest(string BoundaryWkt, IReadOnlyCollection<Guid> AssetIds);
public record MissionAssignmentRequest(Guid UserId, string AssignmentRole);
public record MissionDroneRequest(Guid DroneId);
public record MissionHandoverRequest(Guid DroneId, Guid ReceivedBy, string Condition, bool Accepted);

public record UpdateMissionRequest(
    string Title,
    string RouteData,
    Guid AssignedToUserId,
    string DroneCode,
    string Status,
    string? Description);
