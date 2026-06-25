using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Infrastructure.Services;

public class CurrentUserServices : ICurrentUserServices
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserServices(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
    }
    
    public string? UserName => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
    
    public IReadOnlyList<string> Roles => _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role)
        .Select(x => x.Value)
        .ToList() ?? new List<string>();
    
    public bool IsAuthenticated =>  _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

    public string? UserAgent => _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString();
}