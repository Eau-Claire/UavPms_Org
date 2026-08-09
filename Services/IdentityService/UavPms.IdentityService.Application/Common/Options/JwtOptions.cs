using System.ComponentModel.DataAnnotations;

namespace UavPms.IdentityService.Application.Common.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    
    [Required(ErrorMessage = "Jwt:Secret is required.")]
    public string SecretKey { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Jwt:Issuer is required.")]
    public string Issuer { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Jwt:Audience is required.")]
    public string Audience { get; set; } = string.Empty;
    
    [Range(1, 10080, ErrorMessage = "Jwt:ExpirationInMinutes must be between 1 minute and 1 week.")]
    public int ExpiryMinutes { get; set; } = 60;
}