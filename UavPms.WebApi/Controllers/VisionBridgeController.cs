using System;
using System.IO;
using System.Threading.Tasks;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UavPms.Application.Features.VisionBridge.Commands;
using UavPms.Application.Features.VisionBridge.DTOs;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/vision")]
[ApiVersion("1.0")]
public class VisionBridgeController : ControllerBase
{
    private readonly ISender _mediator;

    public VisionBridgeController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("detections")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ReceiveDetection(
        [FromForm] string drone_id,
        [FromForm] string class_name,
        [FromForm] double confidence,
        [FromForm] string timestamp,
        [FromForm] double lat,
        [FromForm] double lng,
        [FromForm] int? track_id,
        IFormFile? image)
    {
        DateTime parsedTimestamp;
        if (!DateTime.TryParse(timestamp, out parsedTimestamp))
        {
            parsedTimestamp = DateTime.UtcNow;
        }

        var detection = new VisionDetectionDto
        {
            DroneId = drone_id,
            ClassName = class_name,
            Confidence = Math.Min(confidence, 1.0),
            Timestamp = parsedTimestamp,
            Latitude = lat,
            Longitude = lng,
            TrackId = track_id ?? 0,
            ImageName = image?.FileName
        };

        var command = new ReceiveVisionDetectionCommand
        {
            Detection = detection
        };

        if (image != null && image.Length > 0)
        {
            command.EvidenceImageStream = image.OpenReadStream();
            command.EvidenceFileName = image.FileName;
        }

        var result = await _mediator.Send(command);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("detections/json")]
    public async Task<IActionResult> ReceiveDetectionJson(
        [FromBody] VisionDetectionDto detection)
    {
        var command = new ReceiveVisionDetectionCommand
        {
            Detection = detection
        };

        var result = await _mediator.Send(command);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "ok",
            service = "UavPms Vision Bridge",
            timestamp = DateTime.UtcNow
        });
    }
}
