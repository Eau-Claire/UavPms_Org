namespace UavPms.IdentityService.Application.Features.Auth.DTOs;

public class OtpVerifyResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public AuthResultDto? AuthResult { get; set; }
}