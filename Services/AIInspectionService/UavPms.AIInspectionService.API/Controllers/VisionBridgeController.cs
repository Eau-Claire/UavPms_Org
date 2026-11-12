using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UavPms.AIInspectionService.Application.Features.VisionBridge.Commands;
using UavPms.AIInspectionService.Application.Features.VisionBridge.DTOs;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.AIInspectionService.API.Controllers;

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
    [AllowAnonymous]
    [VisionBridgeAuth]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ReceiveDetection(
        [FromForm] string drone_id,
        [FromForm] string class_name,
        [FromForm] double confidence,
        [FromForm] string timestamp,
        [FromForm] double lat,
        [FromForm] double lng,
        [FromForm] int? track_id,
        [FromForm] string? bbox,
        IFormFile? image)
    {
        DateTime parsedTimestamp;
        if (!DateTime.TryParse(timestamp, out parsedTimestamp))
        {
            parsedTimestamp = DateTime.UtcNow;
        }

        int[]? parsedBbox = null;
        if (!string.IsNullOrEmpty(bbox))
        {
            try
            {
                var parts = bbox.Split(',');
                if (parts.Length == 4)
                {
                    parsedBbox = new int[] {
                        int.Parse(parts[0]),
                        int.Parse(parts[1]),
                        int.Parse(parts[2]),
                        int.Parse(parts[3])
                    };
                }
            }
            catch {}
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
            BoundingBox = parsedBbox,
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
    [AllowAnonymous]
    [VisionBridgeAuth]
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
    [Authorize(Roles = UserRoles.AdminOnly)]
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

/// <summary>
/// Allows either an authenticated JWT user or an Edge Vision device presenting a valid API Key
/// (X-Api-Key / X-Service-Key matching Vision:ApiKey or AIService:ServiceKey).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class VisionBridgeAuthAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            return;
        }

        var config = context.HttpContext.RequestServices.GetService<IConfiguration>();
        var configuredKey = config?["Vision:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            configuredKey = config?["AIService:ServiceKey"];
        }

        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            var providedKey = context.HttpContext.Request.Headers["X-Api-Key"].FirstOrDefault()
                              ?? context.HttpContext.Request.Headers["X-Service-Key"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(providedKey) &&
                FixedTimeEquals(providedKey, configuredKey))
            {
                return;
            }
        }

        context.Result = new UnauthorizedObjectResult(new ApiResponse(
            false,
            "Authentication required: provide a valid JWT Bearer token or Edge API Key.",
            ErrorCode: "UNAUTHORIZED"));
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        return bytesA.Length == bytesB.Length && CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }
}
