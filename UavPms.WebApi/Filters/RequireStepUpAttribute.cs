using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using UavPms.Core.Interfaces.Services;
using UavPms.WebApi.Controllers;

namespace UavPms.WebApi.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireStepUpAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _purpose;

    public RequireStepUpAttribute(string purpose)
    {
        _purpose = purpose;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        
        if (!request.Headers.TryGetValue("X-StepUp-Token", out var tokenValues))
        {
            context.Result = new UnauthorizedObjectResult(new ApiResponse(false, "Step-Up token missing. Please verify OTP first."));
            return;
        }

        var tokenString = tokenValues.FirstOrDefault();
        if (string.IsNullOrEmpty(tokenString))
        {
            context.Result = new UnauthorizedObjectResult(new ApiResponse(false, "Step-Up token is empty."));
            return;
        }

        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var secretKey = configuration["Jwt:SecretKey"];
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(tokenString, validationParameters, out var validatedToken);

            var purposeClaim = principal.Claims.FirstOrDefault(c => c.Type == "step_up_purpose");
            if (purposeClaim == null || purposeClaim.Value != _purpose)
            {
                context.Result = new ObjectResult(new ApiResponse(false, "Step-Up token purpose mismatch.")) { StatusCode = 403 };
                return;
            }

            var verifiedAtClaim = principal.Claims.FirstOrDefault(c => c.Type == "step_up_verified_at");
            if (verifiedAtClaim == null)
            {
                context.Result = new UnauthorizedObjectResult(new ApiResponse(false, "Invalid Step-Up token missing verified_at."));
                return;
            }

            var userIdString = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
            {
                context.Result = new UnauthorizedObjectResult(new ApiResponse(false, "Invalid Step-Up token missing user identifier."));
                return;
            }
            
            // Verify with Redis key step-up:{userId}:{purpose}
            var authenticatedUserId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if(string.IsNullOrEmpty(authenticatedUserId) || authenticatedUserId != userIdString)
            {
                context.Result = new ObjectResult(new ApiResponse(false, "Step-Up token is invalid.")) { StatusCode = 403 };
                return;
            }

            var otpService = context.HttpContext.RequestServices.GetRequiredService<IOtpService>();
            var savedToken = await otpService.GetStepUpTokenAsync(userIdString, _purpose);
            if (savedToken == null || savedToken != tokenString)
            {
                context.Result = new ObjectResult(new ApiResponse(false, "Step-Up token has expired, been used, or is invalid.")) { StatusCode = 403 };
                return;
            }
            
            // Store principal in HttpContext items if needed by the controller
            context.HttpContext.Items["StepUpPrincipal"] = principal;
        }
        catch (SecurityTokenExpiredException)
        {
            context.Result = new UnauthorizedObjectResult(new ApiResponse(false, "Step-Up token expired. Please verify OTP again."));
        }
        catch (Exception)
        {
            context.Result = new UnauthorizedObjectResult(new ApiResponse(false, "Invalid Step-Up token."));
        }
    }
}
