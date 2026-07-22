using Microsoft.AspNetCore.Mvc;
using UavPms.Core.Enums;
using Asp.Versioning;
using MediatR;
using UavPms.Application.Features.Auth.Commands.Login;
using UavPms.Application.Features.Auth.Commands.RefreshToken;
using UavPms.Application.Features.Auth.Commands.ResetPassword;
using UavPms.Application.Features.Auth.Commands.SendOtp;
using UavPms.Application.Features.Auth.Commands.VerifyOtp;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1.0")]
public class AuthController : ControllerBase
{
    private readonly ISender _mediator;

    public AuthController(ISender mediator)
    {
        _mediator = mediator;
    }
    
    private void AppendDeviceTrustCookie(string? deviceTrustToken)
    {
        if (string.IsNullOrEmpty(deviceTrustToken)) return;

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var isDev = string.IsNullOrEmpty(env) || env.Equals("Development", StringComparison.OrdinalIgnoreCase);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDev,
            SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None,
            Expires = DateTime.UtcNow.AddDays(30)
        };
        Response.Cookies.Append("device_trust_token", deviceTrustToken, cookieOptions);
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var deviceTrustToken = Request.Cookies["device_trust_token"] 
            ?? Request.Headers["X-Device-Trust-Token"].ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();
        var command = new LoginCommand(request.Username, request.Password, deviceTrustToken, userAgent);
        var result  = await _mediator.Send(command);

        if (result.OtpRequired)
        {
            return Ok(new ApiResponse(true, "OTP required", new { result.Email }));
        }

        AppendDeviceTrustCookie(result.DeviceTrustToken);

        return Ok(new ApiResponse(true, "Success", new
        {
            result.AccessToken,
            result.TokenType,
            result.RefreshToken,
            result.ExpiresIn,
            result.DeviceTrustToken,
            result.User
        }));
    }

    [HttpPost("otp/send")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        var command = new SendOtpCommand(request.Email, request.Purpose, true);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "OTP sent successfully."));
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var userAgent = Request.Headers["User-Agent"].ToString();
        var command = new VerifyOtpCommand(request.Email, request.Otp, request.Purpose, userAgent);
        var result  = await _mediator.Send(command);
        
        if (result.Success && result.AuthResult != null)
        {
            AppendDeviceTrustCookie(result.AuthResult.DeviceTrustToken);
        }
        
        return Ok(new ApiResponse(true, "Verification success.", result));
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var userAgent = Request.Headers["User-Agent"].ToString();
        var command = new RefreshTokenCommand(request.RefreshToken, userAgent);
        var result = await _mediator.Send(command);

        return Ok(new ApiResponse(true, "Token refreshed successfully.", new
        {
            result.AccessToken,
            result.TokenType,
            result.RefreshToken,
            result.ExpiresIn,
            result.User
        }));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var command = new ResetPasswordCommand(request.VerificationToken, request.NewPassword);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Password reset successfully."));
    }
}


public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public record RefreshTokenRequest(string RefreshToken);
public record SendOtpRequest(string Email, OtpPurpose Purpose);
public record VerifyOtpRequest(string Email, string Otp, OtpPurpose Purpose);
public record ResetPasswordRequest(string VerificationToken, string NewPassword);
