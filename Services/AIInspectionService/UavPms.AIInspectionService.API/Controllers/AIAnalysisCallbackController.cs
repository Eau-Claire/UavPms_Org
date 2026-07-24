using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;

namespace UavPms.AIInspectionService.API.Controllers;

/// <summary>
/// Callback API for AI Service to submit inference results.
/// </summary>
[ApiController]
[Route("api/internal/ai-analysis")]
public class AIAnalysisCallbackController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIAnalysisCallbackController> _logger;

    public AIAnalysisCallbackController(
        ISender mediator,
        IConfiguration configuration,
        ILogger<AIAnalysisCallbackController> logger)
    {
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Processes AI inference results callback.
    /// </summary>
    [HttpPost("results")]
    public async Task<IActionResult> ProcessResults([FromBody] ProcessAiAnalysisResultCommand command)
    {
        _logger.LogInformation("Callback received on /api/internal/ai-analysis/results for RequestId={RequestId}", command.RequestId);

        // 1. Authenticate the caller
        if (!AuthenticateCaller())
        {
            _logger.LogWarning("Unauthorized access attempt to AI callback endpoint for RequestId={RequestId}", command.RequestId);
            return StatusCode(StatusCodes.Status401Unauthorized, new ApiResponse(false, "Unauthorized access. Invalid API key or service token."));
        }

        // 2. Process results
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    private bool AuthenticateCaller()
    {
        // Read expected service key from configuration. Do not fall back to a built-in default;
        // callbacks must fail closed when service-to-service auth is not configured.
        var expectedKey = _configuration["AIService:ServiceKey"];
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            _logger.LogError("AI callback authentication is not configured. Set AIService:ServiceKey.");
            return false;
        }

        // Check header: X-AI-Service-Key
        if (Request.Headers.TryGetValue("X-AI-Service-Key", out var headerKey))
        {
            if (string.Equals(headerKey.ToString(), expectedKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        // Check header: Authorization. Only an exact service key is accepted here;
        // user JWTs must not authorize this internal callback endpoint.
        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authStr = authHeader.ToString();
            if (authStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authStr.Substring(7).Trim();
                if (string.Equals(token, expectedKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
