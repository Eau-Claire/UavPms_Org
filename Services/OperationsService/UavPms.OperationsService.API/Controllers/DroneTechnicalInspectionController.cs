using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/drone-technical-inspections")]
[Authorize]
public sealed class DroneTechnicalInspectionController : ControllerBase
{
    private readonly IDroneTechnicalInspectionService _inspectionService;

    public DroneTechnicalInspectionController(IDroneTechnicalInspectionService inspectionService)
    {
        _inspectionService = inspectionService;
    }

    [HttpPost]
    [Authorize(Roles = $"{UserRoles.MaintenanceTechnician},Technician,{UserRoles.Manager},{UserRoles.SystemAdmin}")]
    public async Task<IActionResult> SubmitInspection([FromBody] DroneInspectionSubmitRequest request, CancellationToken ct)
    {
        var inspection = await _inspectionService.SubmitInspectionAsync(request, ct);
        return Ok(inspection.ToDto());
    }

    [HttpGet("drone/{droneId:guid}/latest")]
    [HttpGet("/api/v{version:apiVersion}/uavs/{droneId:guid}/inspections/latest")]
    [HttpGet("/api/v{version:apiVersion}/uavs/{droneId:guid}/inspections")]
    public async Task<IActionResult> GetLatestForDrone(Guid droneId, CancellationToken ct)
    {
        var inspection = await _inspectionService.GetLatestInspectionAsync(droneId, ct);
        if (inspection == null)
            return NotFound(new { message = $"No technical inspection found for drone {droneId}." });

        return Ok(inspection.ToDto());
    }

    [HttpPost("/api/v{version:apiVersion}/uavs/{droneId:guid}/inspections")]
    [Authorize(Roles = $"{UserRoles.MaintenanceTechnician},Technician,{UserRoles.Manager},{UserRoles.SystemAdmin}")]
    public async Task<IActionResult> SubmitUavInspection(Guid droneId, [FromBody] DroneInspectionSubmitRequest request, CancellationToken ct)
    {
        if (request.DroneId == Guid.Empty)
            request = request with { DroneId = droneId };
        else if (request.DroneId != droneId)
            throw new BusinessRuleException("DRONE_ID_MISMATCH", "The droneId in the body does not match the UAV in the route.");

        var inspection = await _inspectionService.SubmitInspectionAsync(request, ct);
        return Ok(inspection.ToDto());
    }
}
