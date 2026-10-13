using System;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Drones;
using UavPms.OperationsService.Application.Features.Drones.DTOs;
using UavPms.OperationsService.Application.Features.Drones.Queries.GetDrones;
using UavPms.OperationsService.Application.Features.Drones.Queries.GetDroneStatus;
using UavPms.OperationsService.Application.Features.FlightControllers;
using UavPms.OperationsService.Application.Features.FlightControllers.DTOs;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/drones")]
[Route("api/drones")]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
public class DronesController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IFlightControllerService _flightControllerService;
    private readonly IUavManagementService _uavManagementService;

    public DronesController(
        ISender mediator,
        IFlightControllerService flightControllerService,
        IUavManagementService uavManagementService)
    {
        _mediator = mediator;
        _flightControllerService = flightControllerService;
        _uavManagementService = uavManagementService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDrones(
        [FromQuery] string? search,
        [FromQuery] string? operationalStatus,
        [FromQuery] string? technicalHealth,
        [FromQuery] Guid? regionId,
        [FromQuery] bool? availableOnly,
        [FromQuery] bool detailed = false,
        CancellationToken ct = default)
    {
        if (detailed || !string.IsNullOrWhiteSpace(search) || !string.IsNullOrWhiteSpace(operationalStatus) ||
            !string.IsNullOrWhiteSpace(technicalHealth) || regionId.HasValue)
        {
            var detailedDrones = await _uavManagementService.GetDronesAsync(
                search, operationalStatus, technicalHealth, regionId, availableOnly, ct);
            return Ok(detailedDrones);
        }

        return Ok(await _mediator.Send(new GetDronesQuery(availableOnly ?? false), ct));
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableDrones(CancellationToken ct = default)
    {
        return Ok(await _mediator.Send(new GetDronesQuery(AvailableOnly: true), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDrone(Guid id, [FromQuery] bool live = false, CancellationToken ct = default)
    {
        if (live)
        {
            return Ok(await _mediator.Send(new GetDroneStatusQuery(id), ct));
        }

        return Ok(await _uavManagementService.GetDroneByIdAsync(id, ct));
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetDroneStatus(Guid id, CancellationToken ct = default)
    {
        return Ok(await _mediator.Send(new GetDroneStatusQuery(id), ct));
    }

    [HttpPost]
    [Authorize(Roles = $"{UserRoles.Manager},{UserRoles.SystemAdmin},{UserRoles.MaintenanceTechnician},Technician")]
    public async Task<ActionResult<UavDetailsDto>> CreateDrone(
        [FromBody] CreateUavRequest request,
        CancellationToken ct)
    {
        var drone = await _uavManagementService.CreateDroneAsync(request, ct);
        return CreatedAtAction(nameof(GetDrone), new { id = drone.Id }, drone);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{UserRoles.Manager},{UserRoles.SystemAdmin},{UserRoles.MaintenanceTechnician},Technician")]
    public async Task<ActionResult<UavDetailsDto>> UpdateDrone(
        Guid id,
        [FromBody] UpdateUavRequest request,
        CancellationToken ct)
    {
        var updated = await _uavManagementService.UpdateDroneAsync(id, request, ct);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{UserRoles.Manager},{UserRoles.SystemAdmin}")]
    public async Task<IActionResult> DeleteDrone(Guid id, CancellationToken ct)
    {
        await _uavManagementService.DeleteDroneAsync(id, ct);
        return Ok(new { message = $"Đã xóa Drone {id} thành công.", droneId = id });
    }

    [HttpGet("{id:guid}/technical-inspections")]
    public async Task<IActionResult> GetTechnicalInspections(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var inspections = await _uavManagementService.GetTechnicalInspectionsForDroneAsync(id, page, pageSize, ct);
        return Ok(inspections);
    }

    [HttpPost("{id:guid}/basic-technical-check")]
    [Authorize(Roles = $"{UserRoles.MaintenanceTechnician},Technician,{UserRoles.Manager},{UserRoles.SystemAdmin}")]
    public async Task<ActionResult<DroneTechnicalInspectionSummaryDto>> SubmitBasicTechnicalCheck(
        Guid id,
        [FromBody] BasicTechnicalCheckRequest request,
        CancellationToken ct)
    {
        var result = await _uavManagementService.SubmitBasicTechnicalCheckAsync(id, request, ct);
        return Ok(result);
    }

    [HttpGet("eligible")]
    public async Task<IActionResult> GetEligibleDrones(
        [FromQuery] Guid assessmentId,
        CancellationToken ct)
    {
        var eligible = await _uavManagementService.GetEligibleDronesForAssessmentAsync(assessmentId, ct);
        return Ok(eligible);
    }

    [HttpPost("estimate-gis-scope")]
    public async Task<ActionResult<GisDroneEstimationResult>> EstimateGisScope(
        [FromBody] GisDroneEstimationRequest request,
        CancellationToken ct)
    {
        var result = await _uavManagementService.EstimateGisScopeDronesAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("estimate-gis-scope")]
    public async Task<ActionResult<GisDroneEstimationResult>> EstimateGisScopeGet(
        [FromQuery] Guid? assessmentId,
        [FromQuery] double? corridorLengthKm,
        [FromQuery] int? assetCount,
        [FromQuery] Guid? regionId,
        CancellationToken ct)
    {
        var request = new GisDroneEstimationRequest
        {
            AssessmentId = assessmentId,
            CorridorLengthKm = corridorLengthKm,
            AssetCount = assetCount,
            RegionId = regionId
        };
        var result = await _uavManagementService.EstimateGisScopeDronesAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("{droneId:guid}/flight-controller")]
    [Authorize(Roles = $"{UserRoles.MaintenanceTechnician},Technician,{UserRoles.Manager},{UserRoles.SystemAdmin}")]
    public async Task<ActionResult<FlightControllerBindingDto>> PairFlightController(
        Guid droneId,
        [FromBody] PairFlightControllerRequest request,
        CancellationToken ct)
    {
        var binding = await _flightControllerService.PairFlightControllerAsync(droneId, request, ct);
        return Ok(binding);
    }

    [HttpDelete("{droneId:guid}/flight-controller")]
    [Authorize(Roles = $"{UserRoles.MaintenanceTechnician},Technician,{UserRoles.Manager},{UserRoles.SystemAdmin}")]
    public async Task<IActionResult> UnpairFlightController(Guid droneId, CancellationToken ct)
    {
        var result = await _flightControllerService.UnpairFlightControllerAsync(droneId, ct);
        if (!result)
            return NotFound(new { message = $"Không tìm thấy Flight Controller đang hoạt động nào cho drone {droneId}." });

        return Ok(new { message = "Đã ngắt kết nối Flight Controller thành công.", droneId });
    }

    [HttpGet("{droneId:guid}/flight-controller")]
    public async Task<ActionResult<FlightControllerBindingDto>> GetActiveFlightController(Guid droneId, CancellationToken ct)
    {
        var binding = await _flightControllerService.GetActiveBindingForDroneAsync(droneId, ct);
        if (binding == null)
            return NotFound(new { message = $"Drone {droneId} chưa được ghép nối với Flight Controller nào." });

        return Ok(binding);
    }

    [HttpGet("{droneId:guid}/flight-controller/history")]
    public async Task<IActionResult> GetFlightControllerHistory(Guid droneId, CancellationToken ct)
    {
        var history = await _flightControllerService.GetBindingHistoryForDroneAsync(droneId, ct);
        return Ok(history);
    }
}
