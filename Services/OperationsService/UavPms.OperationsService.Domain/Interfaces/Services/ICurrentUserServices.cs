namespace UavPms.OperationsService.Domain.Interfaces.Services;

public interface ICurrentUserServices
{
    Guid UserId { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}