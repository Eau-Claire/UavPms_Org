using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
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
        return Ok(inspection);
    }

    [HttpGet("drone/{droneId:guid}/latest")]
    public async Task<IActionResult> GetLatestForDrone(Guid droneId, CancellationToken ct)
    {
        var inspection = await _inspectionService.GetLatestInspectionAsync(droneId, ct);
        if (inspection == null)
            return NotFound(new { message = $"No technical inspection found for drone {droneId}." });

        return Ok(inspection);
    }
}
