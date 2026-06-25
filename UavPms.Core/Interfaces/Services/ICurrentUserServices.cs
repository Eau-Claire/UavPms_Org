namespace UavPms.Core.Interfaces.Services;

public interface ICurrentUserServices
{
    Guid UserId { get; }
    string? UserName { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}