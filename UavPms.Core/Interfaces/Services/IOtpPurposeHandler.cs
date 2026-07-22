using System.Security.Claims;
using System.Threading.Tasks;
using UavPms.Core.Enums;

namespace UavPms.Core.Interfaces.Services;

public class PreconditionResult
{
    public bool IsValid { get; }
    public string Message { get; }
    public bool ShouldSilentSuccess { get; }
    public string? ResolvedEmail { get; }

    public static PreconditionResult Success(string? resolvedEmail = null) => new(true, string.Empty, resolvedEmail: resolvedEmail);
    public static PreconditionResult Failure(string message) => new(false, message);
    public static PreconditionResult SilentSuccess(string? resolvedEmail = null) => new(true, string.Empty, true, resolvedEmail: resolvedEmail);

    private PreconditionResult(bool isValid, string message, bool shouldSilentSuccess = false, string? resolvedEmail = null)
    {
        IsValid = isValid;
        Message = message;
        ShouldSilentSuccess = shouldSilentSuccess;
        ResolvedEmail = resolvedEmail;
    }
}

public interface IOtpPurposeHandler
{
    OtpPurpose Purpose { get; }
    bool RequiresAuthentication { get; }
    Task<PreconditionResult> ValidatePreconditionAsync(string? email, ClaimsPrincipal? currentUser);
}
