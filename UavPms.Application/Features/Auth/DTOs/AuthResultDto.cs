namespace UavPms.Application.Features.Auth.DTOs;
public class AuthResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool OtpRequired { get; set; }
    public string? Email { get; set; }
    public string? AccessToken { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public string? RefreshToken { get; set; }
    public int? ExpiresIn { get; set; }
    public AuthUserDto? User { get; set; }
    public string? DeviceTrustToken { get; set; }

    public static AuthResultDto SuccessResult(string accessToken, string refreshToken, int expiresIn,
        AuthUserDto user, string? deviceTrustToken = null)
    {
        return new AuthResultDto
        {
            Success = true,
            Message = "Success",
            AccessToken = accessToken,
            TokenType = "Bearer",
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            User = user,
            DeviceTrustToken = deviceTrustToken
        };
    }

    public static AuthResultDto OtpRequiredResult(string email)
    {
        return new AuthResultDto
        {
            Success = true,
            Message = "Otp required",
            OtpRequired = true,
            Email = email
        };
    }
}

