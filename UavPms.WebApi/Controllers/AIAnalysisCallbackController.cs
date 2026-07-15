using System;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using UavPms.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;

namespace UavPms.WebApi.Controllers;

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
        // Read expected service key from configuration
        var expectedKey = _configuration["AIService:ServiceKey"] ?? "AI-Service-Secret-Token-Key-12345";

        // Check header: X-AI-Service-Key
        if (Request.Headers.TryGetValue("X-AI-Service-Key", out var headerKey))
        {
            if (string.Equals(headerKey.ToString(), expectedKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        // Check header: Authorization (Bearer token)
        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authStr = authHeader.ToString();
            if (authStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authStr.Substring(7).Trim();
                
                // Check if token matches expected service key
                if (string.Equals(token, expectedKey, StringComparison.Ordinal))
                {
                    return true;
                }

                // Validate token as JWT (service-to-service option)
                if (ValidateJwtToken(token))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool ValidateJwtToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtSettings = _configuration.GetSection("Jwt");
            var secretKey = jwtSettings["SecretKey"];
            if (string.IsNullOrEmpty(secretKey))
            {
                return false;
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.Zero
            };

            tokenHandler.ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate JWT token for service-to-service callback authentication.");
            return false;
        }
    }
}
